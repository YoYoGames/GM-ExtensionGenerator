using System.Collections.Immutable;

namespace extgen.Models
{
    public sealed record IrStruct(string Name, ImmutableArray<IrField> Fields, ImmutableArray<IrFunction> Functions, string? Description = null, bool Hidden = false);
}
