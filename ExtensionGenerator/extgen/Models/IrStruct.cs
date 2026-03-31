using System.Collections.Immutable;

namespace extgen.Models
{
    /// <summary>
    /// Represents a struct in the intermediate representation with fields and member functions.
    /// </summary>
    public sealed record IrStruct(string Name, ImmutableArray<IrField> Fields, ImmutableArray<IrFunction> Functions, string? Description = null, bool Hidden = false);
}
