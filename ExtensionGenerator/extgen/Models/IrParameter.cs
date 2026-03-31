using codegencore.Models;

namespace extgen.Models
{
    /// <summary>
    /// Represents a function parameter in the intermediate representation.
    /// </summary>
    public sealed record IrParameter(string Name, IrType Type, bool IsOptional)
    {
        /// <summary>
        /// Creates a "self" parameter with the specified type.
        /// </summary>
        /// <param name="Type">The type of the self parameter.</param>
        /// <returns>A new IrParameter representing a self parameter.</returns>
        public static IrParameter Self(IrType Type) => new("self", Type, false);
    }

}
