using codegencore.Models;
using extgen.Models;

namespace extgen.Parsing.Validation
{
    /// <summary>
    /// Categorizes where a type is used within the IR compilation.
    /// </summary>
    public enum IrTypeOwnerKind
    {
        /// <summary>Type used in a compilation constant.</summary>
        CompilationConstant,

        /// <summary>Type used as an enum underlying type.</summary>
        EnumUnderlying,

        /// <summary>Type used in a struct field.</summary>
        StructField,

        /// <summary>Type used as a function return type.</summary>
        FunctionReturn,

        /// <summary>Type used as a function parameter.</summary>
        FunctionParameter,
    }

    /// <summary>
    /// Represents a single occurrence of a type within the IR compilation.
    /// </summary>
    public readonly record struct IrTypeOccurrence(
        IrType Type,
        string Path,
        IrTypeOwnerKind OwnerKind,
        string? OwnerName = null,
        string? MemberName = null
    );

    /// <summary>
    /// Utilities for walking the IR tree and finding all type occurrences.
    /// </summary>
    public static class IrWalkers
    {
        /// <summary>
        /// Walks an IR compilation and yields all type occurrences.
        /// </summary>
        public static IEnumerable<IrTypeOccurrence> WalkIrTypes(IrCompilation comp)
        {
            foreach (var c in comp.Constants)
                foreach (var occ in WalkType(c.Type,
                    $"Constants[{c.Name}].Type",
                    IrTypeOwnerKind.CompilationConstant,
                    ownerName: null,
                    memberName: c.Name))
                    yield return occ;

            // Enums: underlying (from IrEnum, not from IrType)
            foreach (var e in comp.Enums)
                foreach (var occ in WalkType(e.Underlying,
                    $"Enums[{e.Name}].Underlying",
                    IrTypeOwnerKind.EnumUnderlying,
                    ownerName: e.Name,
                    memberName: null))
                    yield return occ;

            // Struct fields
            foreach (var s in comp.Structs)
            {
                foreach (var f in s.Fields)
                    foreach (var occ in WalkType(f.Type,
                        $"Structs[{s.Name}].Fields[{f.Name}].Type",
                        IrTypeOwnerKind.StructField,
                        ownerName: s.Name,
                        memberName: f.Name))
                        yield return occ;

                // Functions: return + parameters
                foreach (var fn in s.Functions)
                {
                    foreach (var occ in WalkType(fn.ReturnType,
                        $"Structs[{s.Name}].Functions[{fn.Name}].ReturnType",
                        IrTypeOwnerKind.FunctionReturn,
                        ownerName: fn.Name,
                        memberName: null))
                        yield return occ;

                    foreach (var p in fn.Parameters)
                        foreach (var occ in WalkType(p.Type,
                            $"Structs[{s.Name}].Functions[{fn.Name}].Parameters[{p.Name}].Type",
                            IrTypeOwnerKind.FunctionParameter,
                            ownerName: fn.Name,
                            memberName: p.Name))
                            yield return occ;
                }
            }


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
            yield return new IrTypeOccurrence(type, path, ownerKind, ownerName, memberName);

            switch (type)
            {
                case IrType.Nullable n:
                    foreach (var occ in WalkType(
                        n.Underlying,
                        $"{path}.Underlying",
                        ownerKind,
                        ownerName,
                        memberName))
                        yield return occ;
                    break;

                case IrType.Array a:
                    foreach (var occ in WalkType(
                        a.Element,
                        $"{path}.Element",
                        ownerKind,
                        ownerName,
                        memberName))
                        yield return occ;
                    break;

                    // Builtin / Named have no children
            }
        }
    }

}
