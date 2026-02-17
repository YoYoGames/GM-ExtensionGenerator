using extgen.Models.Utils;
using System.Collections.Immutable;

namespace extgen.Models
{
    public sealed record IrCompilation(string Name, ImmutableArray<IrEnum> Enums, ImmutableArray<IrStruct> Structs, ImmutableArray<IrFunction> Functions, ImmutableArray<IrConstant> Constants) 
    {
        public IEnumerable<IrFunction> GetAllFunctions(Func<IrStruct, IrFunction, IrFunction> patcher)
        {
            return Functions.Concat(
                Structs.SelectMany(s => s.Functions.Select(f => patcher(s, f)))
            );
        }
    }
}
