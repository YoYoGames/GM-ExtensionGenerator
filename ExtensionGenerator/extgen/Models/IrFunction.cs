using codegencore.Models;
using System.Collections.Immutable;

namespace extgen.Models
{
    public sealed record IrFunction(string Name, IrType ReturnType, ImmutableArray<IrParameter> Parameters);

}
