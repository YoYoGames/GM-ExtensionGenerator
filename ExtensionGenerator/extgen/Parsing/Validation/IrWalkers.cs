using extgen.Model;

namespace extgen.Parsing.Validation
{
    public enum IrTypeOwnerKind
    {
        CompilationConstant,
        EnumUnderlying,
        StructField,
        FunctionReturn,
        FunctionParameter,
    }

    public readonly record struct IrTypeOccurrence(
        IrType Type,
        string Path,
        IrTypeOwnerKind OwnerKind,
        string? OwnerName = null,
        string? MemberName = null
    );

    public static class IrWalkers
    {
        public static IEnumerable<IrTypeOccurrence> WalkIrTypes(IrCompilation comp)
        {
            // Constants
            foreach (var c in comp.Constants)
                foreach (var occ in WalkType(c.Type,
                    $"Constants[{c.Name}].Type",
                    IrTypeOwnerKind.CompilationConstant,
                    ownerName: null,
                    memberName: c.Name))
                    yield return occ;

            // Enums: underlying
            foreach (var e in comp.Enums)
                foreach (var occ in WalkType(e.Underlying,
                    $"Enums[{e.Name}].Underlying",
                    IrTypeOwnerKind.EnumUnderlying,
                    ownerName: e.Name,
                    memberName: null))
                    yield return occ;

            // Struct fields
            foreach (var s in comp.Structs)
                foreach (var f in s.Fields)
                    foreach (var occ in WalkType(f.Type,
                        $"Structs[{s.Name}].Fields[{f.Name}].Type",
                        IrTypeOwnerKind.StructField,
                        ownerName: s.Name,
                        memberName: f.Name))
                        yield return occ;

            // Functions: return + parameters
            foreach (var fn in comp.Functions)
            {
                foreach (var occ in WalkType(fn.ReturnType,
                    $"Functions[{fn.Name}].ReturnType",
                    IrTypeOwnerKind.FunctionReturn,
                    ownerName: fn.Name,
                    memberName: null))
                    yield return occ;

                foreach (var p in fn.Parameters)
                    foreach (var occ in WalkType(p.Type,
                        $"Functions[{fn.Name}].Parameters[{p.Name}].Type",
                        IrTypeOwnerKind.FunctionParameter,
                        ownerName: fn.Name,
                        memberName: p.Name))
                        yield return occ;
            }
        }

        private static IEnumerable<IrTypeOccurrence> WalkType(
            IrType type,
            string path,
            IrTypeOwnerKind ownerKind,
            string? ownerName,
            string? memberName)
        {
            // This type occurrence
            yield return new IrTypeOccurrence(type, path, ownerKind, ownerName, memberName);

            // Variant arms
            if (type.Variants.Length > 0)
            {
                for (int i = 0; i < type.Variants.Length; i++)
                {
                    var arm = type.Variants[i];
                    foreach (var occ in WalkType(
                        arm,
                        $"{path}.Variants[{i}]",
                        ownerKind,
                        ownerName,
                        memberName))
                        yield return occ;
                }
            }

            // Underlying type (e.g., enum underlying)
            if (type.Underlying is not null)
            {
                foreach (var occ in WalkType(
                    type.Underlying,
                    $"{path}.Underlying",
                    ownerKind,
                    ownerName,
                    memberName))
                    yield return occ;
            }
        }
    }
}
