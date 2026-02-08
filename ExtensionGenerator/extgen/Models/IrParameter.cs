using codegencore.Models;

namespace extgen.Models
{
    public sealed record IrParameter(string Name, IrType Type, bool IsOptional);

}
