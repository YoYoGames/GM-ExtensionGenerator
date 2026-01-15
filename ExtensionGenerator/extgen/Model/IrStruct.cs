using System.Collections.Immutable;

namespace extgen.Model
{
    public sealed record IrStruct(string Name, ImmutableArray<IrField> Fields, string? Description = null);
}
