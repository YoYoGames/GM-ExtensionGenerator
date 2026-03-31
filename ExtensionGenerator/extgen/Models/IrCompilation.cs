using extgen.Models.Utils;
using System.Collections.Immutable;

namespace extgen.Models
{
    /// <summary>
    /// Represents a complete compilation unit containing enums, structs, functions, and constants.
    /// </summary>
    public sealed record IrCompilation(string Name, ImmutableArray<IrEnum> Enums, ImmutableArray<IrStruct> Structs, ImmutableArray<IrFunction> Functions, ImmutableArray<IrConstant> Constants)
    {
        /// <summary>
        /// Gets all functions including top-level functions and struct member functions.
        /// </summary>
        /// <param name="patcher">Function to patch struct member functions with their struct context.</param>
        /// <returns>An enumerable of all functions in the compilation.</returns>
        public IEnumerable<IrFunction> GetAllFunctions(Func<IrStruct, IrFunction, IrFunction> patcher)
        {
            return Functions.Concat(
                Structs.SelectMany(s => s.Functions.Select(f => patcher(s, f)))
            );
        }
    }
}
