using codegencore.Models;
using extgen.Models;
using System.Collections.Immutable;

namespace extgen.Parsing.Validation
{
    public sealed record IrDiagnostic(
        string Code,
        string Message,
        IrSeverity Severity,
        string? Path = null
    );

    public enum IrSeverity { Info, Warning, Error }

    public interface IIrRule
    {
        IEnumerable<IrDiagnostic> Validate(IrCompilation comp);
    }

    public sealed class IrValidator(params IIrRule[] rules)
    {
        private readonly IIrRule[] _rules = rules;

        public ImmutableArray<IrDiagnostic> Validate(IrCompilation comp) =>
            _rules.SelectMany(r => r.Validate(comp)).ToImmutableArray();
    }

    internal static class IrTypeInspection
    {
        public static bool ContainsBuiltin(IrType t, BuiltinKind kind) =>
            t switch
            {
                IrType.Builtin b => b.Kind == kind,
                IrType.Nullable n => ContainsBuiltin(n.Underlying, kind),
                IrType.Array a => ContainsBuiltin(a.Element, kind),
                _ => false
            };
    }

    public sealed class NoDuplicateSymbolsRule : IIrRule
    {
        private readonly StringComparer _cmp;

        public NoDuplicateSymbolsRule(StringComparer? comparer = null)
        {
            _cmp = comparer ?? StringComparer.Ordinal;
        }

        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            // name -> list of (kind, path)
            var map = new Dictionary<string, List<(string Kind, string Path)>>(_cmp);

            void Add(string name, string kind, string path)
            {
                if (!map.TryGetValue(name, out var list))
                {
                    list = new List<(string Kind, string Path)>();
                    map[name] = list;
                }
                list.Add((kind, path));
            }

            // Enums
            for (int i = 0; i < comp.Enums.Length; i++)
            {
                var e = comp.Enums[i];
                Add(e.Name, "enum", $"Enums[{e.Name}]");
            }

            // Structs
            for (int i = 0; i < comp.Structs.Length; i++)
            {
                var s = comp.Structs[i];
                Add(s.Name, "struct", $"Structs[{s.Name}]");
            }

            // Constants
            for (int i = 0; i < comp.Constants.Length; i++)
            {
                var c = comp.Constants[i];
                Add(c.Name, "constant", $"Constants[{c.Name}]");
            }

            // Functions
            for (int i = 0; i < comp.Functions.Length; i++)
            {
                var f = comp.Functions[i];
                Add(f.Name, "function", $"Functions[{f.Name}]");
            }

            foreach (var kv in map)
            {
                var name = kv.Key;
                var occurrences = kv.Value;

                if (occurrences.Count <= 1)
                    continue;

                // Build a helpful message: "enum @ Enums[X], function @ Functions[X]"
                var details = string.Join(", ",
                    occurrences.Select(o => $"{o.Kind} @ {o.Path}"));

                yield return new IrDiagnostic(
                    Code: "IR_SYM_001",
                    Message: $"Duplicate symbol name '{name}' is declared multiple times: {details}.",
                    Severity: IrSeverity.Error,
                    Path: occurrences[0].Path);
            }
        }
    }

    public sealed class NoUnknownTypeAllowedRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            var enums = new HashSet<string>(comp.Enums.Select(e => e.Name), StringComparer.Ordinal);
            var structs = new HashSet<string>(comp.Structs.Select(s => s.Name), StringComparer.Ordinal);

            foreach (var occ in IrWalkers.WalkIrTypes(comp))
            {
                if (occ.Type is not IrType.Named named)
                    continue;

                var ok = named.Kind switch
                {
                    NamedKind.Enum => enums.Contains(named.Name),
                    NamedKind.Struct => structs.Contains(named.Name),
                    _ => false
                };

                if (!ok)
                {
                    yield return new IrDiagnostic(
                        Code: "IR001",
                        Message: $"Unknown {named.Kind.ToString().ToLowerInvariant()} type '{named.Name}' referenced at {occ.Path}.",
                        Severity: IrSeverity.Error,
                        Path: occ.Path);
                }
            }
        }
    }

    public sealed class NoBufferOrFunctionInStructFieldsRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var occ in IrWalkers.WalkIrTypes(comp))
            {
                if (occ.OwnerKind != IrTypeOwnerKind.StructField)
                    continue;

                if (IrTypeInspection.ContainsBuiltin(occ.Type, BuiltinKind.Buffer) ||
                    IrTypeInspection.ContainsBuiltin(occ.Type, BuiltinKind.Function))
                {
                    yield return new IrDiagnostic(
                        Code: "IR011",
                        Message: $"Struct '{occ.OwnerName}' field '{occ.MemberName}' cannot contain 'buffer' or 'function'.",
                        Severity: IrSeverity.Error,
                        Path: occ.Path);
                }
            }
        }
    }

    public sealed class NoUnderscoresInCompilationNameRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            if (comp.Name.Contains('_'))
            {
                yield return new IrDiagnostic(
                    Code: "IR020",
                    Message: $"Module name '{comp.Name}' cannot contain underscores.",
                    Severity: IrSeverity.Error,
                    Path: "ModuleName");
            }
        }
    }

    public sealed class FunctionCommonPrefixRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            if (comp.Functions.Length <= 1)
                yield break;

            static string GetPrefix(string name) => name.Split('_', 2)[0];

            var expected = GetPrefix(comp.Functions[0].Name);

            foreach (var fn in comp.Functions)
            {
                var prefix = GetPrefix(fn.Name);
                if (!StringComparer.Ordinal.Equals(prefix, expected))
                {
                    yield return new IrDiagnostic(
                        Code: "IR030",
                        Message: $"Function '{fn.Name}' does not share expected prefix '{expected}'.",
                        Severity: IrSeverity.Warning,
                        Path: $"Functions[{fn.Name}]");
                }
            }
        }
    }

    public sealed class EnumUnderlyingMustBeIntegralScalarRule : IIrRule
    {
        private static readonly HashSet<BuiltinKind> Allowed = new()
    {
        BuiltinKind.Int8, BuiltinKind.UInt8,
        BuiltinKind.Int16, BuiltinKind.UInt16,
        BuiltinKind.Int32, BuiltinKind.UInt32,
        BuiltinKind.Int64, BuiltinKind.UInt64,
        // optionally: BuiltinKind.Bool (usually no)
    };

        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var e in comp.Enums)
            {
                var t = e.Underlying;

                if (t is IrType.Nullable || t is IrType.Array)
                {
                    yield return new IrDiagnostic(
                        "IR_ENUM_002",
                        $"Enum '{e.Name}' underlying type must not be nullable/array (got '{t}').",
                        IrSeverity.Error,
                        $"Enums[{e.Name}].Underlying");
                    continue;
                }

                if (t is not IrType.Builtin b || !Allowed.Contains(b.Kind))
                {
                    yield return new IrDiagnostic(
                        "IR_ENUM_004",
                        $"Enum '{e.Name}' underlying type must be an integral builtin (got '{t}').",
                        IrSeverity.Error,
                        $"Enums[{e.Name}].Underlying");
                }
            }
        }
    }

    public sealed class EnumMemberNamesMustBeUniqueRule : IIrRule
    {
        private readonly StringComparer _comparer;

        public EnumMemberNamesMustBeUniqueRule(StringComparer? comparer = null)
        {
            _comparer = comparer ?? StringComparer.Ordinal;
        }

        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var enm in comp.Enums)
            {
                var seen = new HashSet<string>(_comparer);

                foreach (var m in enm.Members)
                {
                    if (!seen.Add(m.Name))
                    {
                        yield return new IrDiagnostic(
                            Code: "IR_ENUM_010",
                            Message: $"Enum '{enm.Name}' has duplicate member name '{m.Name}'.",
                            Severity: IrSeverity.Error,
                            Path: $"Enums[{enm.Name}].Members[{m.Name}]");
                    }
                }
            }
        }
    }

    public sealed class NoBufferOrFunctionReturnTypesRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var occ in IrWalkers.WalkIrTypes(comp))
            {
                if (occ.OwnerKind != IrTypeOwnerKind.FunctionReturn)
                    continue;

                if (IrTypeInspection.ContainsBuiltin(occ.Type, BuiltinKind.Buffer) ||
                    IrTypeInspection.ContainsBuiltin(occ.Type, BuiltinKind.Function))
                {
                    yield return new IrDiagnostic(
                        Code: "IR_FUNC_001",
                        Message: $"Function '{occ.OwnerName}' has invalid return type containing buffer/function.",
                        Severity: IrSeverity.Error,
                        Path: occ.Path);
                }
            }
        }
    }

}