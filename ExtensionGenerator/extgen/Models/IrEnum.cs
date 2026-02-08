using codegencore.Models;
using System.Collections.Immutable;

namespace extgen.Models
{
    public sealed record IrEnum(string Name, IrType Underlying, ImmutableArray<IrEnumMember> Members, string? Description = null);
}
