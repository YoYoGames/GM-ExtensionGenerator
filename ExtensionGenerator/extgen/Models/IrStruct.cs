using System.Collections.Immutable;

namespace extgen.Models
{
    public sealed record IrStruct(string Name, ImmutableArray<IrField> Fields, string? Description = null);
}
