using codegencore.Models;

namespace extgen.Models
{
    /// <summary>
    /// Represents a field in a struct, including its type, default value, and metadata.
    /// </summary>
    public sealed record IrField(
        string Name,
        IrType Type,
        string? DefaultLiteral,
        bool Required,
        string? Description = null,
        string? Value = null, bool Hidden = false);

}
