namespace extgen.Models
{
    /// <summary>
    /// Represents a member of an enumeration with its name, default value literal, and description.
    /// </summary>
    public sealed record IrEnumMember(string Name, string? DefaultLiteral, string? Description = null);
}
