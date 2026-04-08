using codegencore.Models;
using extgen.Models;
using System.Collections.Immutable;

namespace extgen.Parsing.Validation
{
    /// <summary>
    /// Represents a diagnostic message from IR validation.
    /// </summary>
    public sealed record IrDiagnostic(
        string Code,
        string Message,
        IrSeverity Severity,
        string? Path = null
    );

    /// <summary>
    /// Diagnostic severity levels.
    /// </summary>
    public enum IrSeverity
    {
        /// <summary>Informational message.</summary>
        Info,

        /// <summary>Warning message.</summary>
        Warning,

        /// <summary>Error message.</summary>
        Error
    }

    /// <summary>
    /// Interface for IR validation rules.
    /// </summary>
    public interface IIrRule
    {
        /// <summary>
        /// Validates an IR compilation and returns diagnostics.
        /// </summary>
        IEnumerable<IrDiagnostic> Validate(IrCompilation comp);
    }

    /// <summary>
    /// Validates IR compilations using a set of rules.
    /// </summary>
    public sealed class IrValidator(params IIrRule[] rules)
    {
        private readonly IIrRule[] _rules = rules;

        /// <summary>
        /// Validates a compilation and returns all diagnostics.
        /// </summary>
        public ImmutableArray<IrDiagnostic> Validate(IrCompilation comp) =>
            [.. _rules.SelectMany(r => r.Validate(comp))];
    }

    /// <summary>
    /// Utilities for inspecting IR type structures.
    /// </summary>
    internal static class IrTypeInspection
    {
        /// <summary>
        /// Checks if a type contains a specific builtin kind (recursively).
        /// </summary>
        public static bool ContainsBuiltin(IrType t, BuiltinKind kind) =>
            t switch
            {
                IrType.Builtin b => b.Kind == kind,
                IrType.Nullable n => ContainsBuiltin(n.Underlying, kind),
                IrType.Array a => ContainsBuiltin(a.Element, kind),
                _ => false
            };
    }

    /// <summary>
    /// Validates that no duplicate symbol names exist in the compilation.
    /// </summary>
    public sealed class NoDuplicateSymbolsRule(StringComparer? comparer = null) : IIrRule
    {
        private readonly StringComparer _cmp = comparer ?? StringComparer.Ordinal;

        /// <inheritdoc />
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

    /// <summary>
    /// Validates that all named types reference existing enum or struct definitions.
    /// </summary>
    public sealed class NoUnknownTypeAllowedRule : IIrRule
    {
        /// <inheritdoc />
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

    /// <summary>
    /// Validates that struct fields do not contain buffer or function types.
    /// </summary>
    public sealed class NoBufferOrFunctionInStructFieldsRule : IIrRule
    {
        /// <inheritdoc />
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

    /// <summary>
    /// Validates that the compilation name does not contain underscores.
    /// </summary>
    public sealed class NoUnderscoresInCompilationNameRule : IIrRule
    {
        /// <inheritdoc />
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

    /// <summary>
    /// Validates that all functions share a common prefix (warning only).
    /// </summary>
    public sealed class FunctionCommonPrefixRule : IIrRule
    {
        /// <inheritdoc />
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            // Check global functions only
            List<IrFunction> fncs = [.. comp.Functions];

            if (fncs.Count <= 1)
                yield break;

            static string GetPrefix(string name) => name.Split('_', 2)[0];

            var expected = GetPrefix(fncs[0].Name);

            foreach (var fn in fncs)
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

    /// <summary>
    /// Validates that struct methods do not have function modifiers.
    /// </summary>
    public sealed class StructMethodsCannotHaveModifiers : IIrRule
    {
        /// <inheritdoc />
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var s in comp.Structs)
            {
                foreach (var fn in s.Functions)
                {
                    if (fn.Modifier != IrFunctionModifier.None)
                    {
                        yield return new IrDiagnostic(
                            Code: "IR_METHOD_0",
                            Message: $"Struct method '{s.Name}.{fn.Name}' cannot have modifiers ('{fn.Modifier}').",
                            Severity: IrSeverity.Error,
                            Path: $"Structs[{s.Name}].Functions[{fn.Name}]");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates that function modifiers (Start, Finish) are only used once.
    /// </summary>
    public sealed class FunctionModifiersMustBeUnique : IIrRule
    {
        /// <inheritdoc />
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            var startFn = comp.Functions.Where(fn => fn.Modifier == IrFunctionModifier.Start).FirstOrDefault();
            var finishFn = comp.Functions.Where(fn => fn.Modifier == IrFunctionModifier.Finish).FirstOrDefault();

            if (startFn == null || finishFn == null) yield break;

            foreach (var fn in comp.Functions)
            {
                if (fn.Modifier == IrFunctionModifier.Start && fn != startFn)
                {
                    yield return new IrDiagnostic(
                        Code: "IR030",
                        Message: $"Function '{fn.Name}' has a duplicated modifier '{fn.Modifier}', first seen on '{startFn.Name}'.",
                        Severity: IrSeverity.Error,
                        Path: $"Functions[{fn.Name}]");
                }

                if (fn.Modifier == IrFunctionModifier.Finish && fn != finishFn)
                {
                    yield return new IrDiagnostic(
                        Code: "IR030",
                        Message: $"Function '{fn.Name}' has a duplicated modifier '{fn.Modifier}', first seen on '{finishFn.Name}'.",
                        Severity: IrSeverity.Error,
                        Path: $"Functions[{fn.Name}]");
                }
            }
        }
    }

    /// <summary>
    /// Validates that enum underlying types are integral scalar types.
    /// </summary>
    public sealed class EnumUnderlyingMustBeIntegralScalarRule : IIrRule
    {
        private static readonly HashSet<BuiltinKind> Allowed =
        [
            BuiltinKind.Int8, BuiltinKind.UInt8,
            BuiltinKind.Int16, BuiltinKind.UInt16,
            BuiltinKind.Int32, BuiltinKind.UInt32,
            BuiltinKind.Int64, BuiltinKind.UInt64,
        ];

        /// <inheritdoc />
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

    /// <summary>
    /// Validates that enum member names are unique within each enum.
    /// </summary>
    public sealed class EnumMemberNamesMustBeUniqueRule(StringComparer? comparer = null) : IIrRule
    {
        private readonly StringComparer _comparer = comparer ?? StringComparer.Ordinal;

        /// <inheritdoc />
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

    /// <summary>
    /// Validates that function return types do not contain buffer or function types.
    /// </summary>
    public sealed class NoBufferOrFunctionReturnTypesRule : IIrRule
    {
        /// <inheritdoc />
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