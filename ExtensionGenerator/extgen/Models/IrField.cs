using codegencore.Models;

namespace extgen.Models
{
    public sealed record IrField(
        string Name,
        IrType Type,
        string? DefaultLiteral,
        bool Required,
        string? Description = null,
        string? Value = null, bool Hidden = false);

}
