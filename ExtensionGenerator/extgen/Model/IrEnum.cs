using System.Collections.Immutable;

namespace extgen.Model
{
    public sealed record IrEnum(string Name, IrType Underlying, ImmutableArray<IrEnumMember> Members, string? Description = null);
}
