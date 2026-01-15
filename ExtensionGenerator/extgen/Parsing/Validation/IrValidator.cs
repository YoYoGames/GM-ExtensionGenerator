using extgen.Model;
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

    public sealed class NoUnknownTypeAllowedRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var occ in IrWalkers.WalkIrTypes(comp))
            {
                if (occ.Type.Kind != IrTypeKind.Error)
                    continue;

                yield return new IrDiagnostic(
                    Code: "IR001",
                    Message: $"Unknown type '{occ.Type.Name}' referenced at {occ.Path}.",
                    Severity: IrSeverity.Error,
                    Path: occ.Path);
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

                if (occ.Type.Kind is not (IrTypeKind.Buffer or IrTypeKind.Function))
                    continue;

                yield return new IrDiagnostic(
                    Code: "IR011",
                    Message: $"Struct '{occ.OwnerName}' field '{occ.MemberName}' cannot be a '{occ.Type.Kind.ToString().ToLowerInvariant()}'.",
                    Severity: IrSeverity.Error,
                    Path: occ.Path);
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

    public sealed class EnumUnderlyingMustBeScalarRule : IIrRule
    {
        // Leave empty if you only want "must be scalar".
        private static readonly HashSet<string> AllowedScalarNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "int8", "uint8",
            "int16", "uint16",
            "int32", "uint32",
            "int64", "uint64"
        };

        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var enm in comp.Enums)
            {
                var t = enm.Underlying;

                if (t.Kind != IrTypeKind.Scalar)
                {
                    yield return new IrDiagnostic(
                        Code: "IR_ENUM_001",
                        Message: $"Enum '{enm.Name}' underlying type must be a scalar, but was '{t.Kind}' ('{t.Name}').",
                        Severity: IrSeverity.Error,
                        Path: $"Enums[{enm.Name}].Underlying");
                    continue;
                }

                if (t.IsCollection || t.FixedLength is not null)
                {
                    yield return new IrDiagnostic(
                        Code: "IR_ENUM_002",
                        Message: $"Enum '{enm.Name}' underlying type must not be a collection (got '{t.Name}[]').",
                        Severity: IrSeverity.Error,
                        Path: $"Enums[{enm.Name}].Underlying");
                }

                if (t.IsNullable)
                {
                    yield return new IrDiagnostic(
                        Code: "IR_ENUM_003",
                        Message: $"Enum '{enm.Name}' underlying type must not be nullable (got '{t.Name}?').",
                        Severity: IrSeverity.Error,
                        Path: $"Enums[{enm.Name}].Underlying");
                }

                if (AllowedScalarNames.Count > 0 && !AllowedScalarNames.Contains(t.Name))
                {
                    yield return new IrDiagnostic(
                        Code: "IR_ENUM_004",
                        Message: $"Enum '{enm.Name}' underlying scalar '{t.Name}' is not allowed. Allowed: {string.Join(", ", AllowedScalarNames)}.",
                        Severity: IrSeverity.Error,
                        Path: $"Enums[{enm.Name}].Underlying");
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

                if (occ.Type.Kind is IrTypeKind.Buffer or IrTypeKind.Function)
                {
                    yield return new IrDiagnostic(
                        Code: "IR_FUNC_001",
                        Message: $"Function '{occ.OwnerName}' has invalid return type '{occ.Type.Kind}' ('{occ.Type.Name}').",
                        Severity: IrSeverity.Error,
                        Path: occ.Path);
                }
            }
        }
    }

    public sealed class NoVariantDataAllowedRule : IIrRule
    {
        public IEnumerable<IrDiagnostic> Validate(IrCompilation comp)
        {
            foreach (var occ in IrWalkers.WalkIrTypes(comp))
            {
                // If you changed Variants to non-null, adapt this accordingly.
                if (occ.Type.Variants is { Length: > 0 })
                {
                    yield return new IrDiagnostic(
                        Code: "IR_VARIANT_001",
                        Message: $"Variant alternatives are not supported yet (found variants at {occ.Path}).",
                        Severity: IrSeverity.Error,
                        Path: occ.Path);
                }
            }
        }
    }
}