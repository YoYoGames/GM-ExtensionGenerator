using codegencore.Models;

namespace extgen.Models
{
    /// <summary>
    /// Represents a constant value in the intermediate representation.
    /// </summary>
    public sealed record IrConstant(string Name, IrType Type, string Literal);
}
