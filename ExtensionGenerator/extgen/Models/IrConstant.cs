using codegencore.Models;

namespace extgen.Models
{
    public sealed record IrConstant(string Name, IrType Type, string Literal);
}
