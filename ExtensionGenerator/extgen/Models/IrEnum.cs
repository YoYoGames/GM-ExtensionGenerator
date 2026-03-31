using codegencore.Models;
using System.Collections.Immutable;

namespace extgen.Models
{
    /// <summary>
    /// Represents an enumeration in the intermediate representation with its underlying type and members.
    /// </summary>
    public sealed record IrEnum(string Name, IrType Underlying, ImmutableArray<IrEnumMember> Members, string? Description = null);
}
