using codegencore.Models;
using System.Collections.Immutable;

namespace extgen.Models
{
    /// <summary>
    /// Specifies the modifier applied to a function.
    /// </summary>
    public enum IrFunctionModifier { None, Start, Finish };

    /// <summary>
    /// Represents a function in the intermediate representation, including its name, return type, parameters, and modifiers.
    /// </summary>
    public sealed record IrFunction(string Name, IrType ReturnType, ImmutableArray<IrParameter> Parameters, IrParameter? Self, bool Hidden = false, IrFunctionModifier Modifier = IrFunctionModifier.None)
    {
        /// <summary>
        /// Gets the full list of parameters including the Self parameter if present.
        /// </summary>
        public ImmutableArray<IrParameter> FullParameters =>
            Self is null ? Parameters : [Self, .. Parameters];
    }

}
