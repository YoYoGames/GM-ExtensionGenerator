using System.Collections.Immutable;

namespace extgen.Models
{
    public sealed record IrCompilation(string Name, ImmutableArray<IrEnum> Enums, ImmutableArray<IrStruct> Structs, ImmutableArray<IrFunction> Functions, ImmutableArray<IrConstant> Constants);
}
