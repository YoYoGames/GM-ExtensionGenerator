using codegencore.Models;

namespace extgen.Models
{
    /// <summary>
    /// Represents a function parameter in the intermediate representation.
    /// </summary>
    /// <param name="GmlDefault">
    /// For an optional parameter: a GML expression the generated wrapper assigns when the argument is undefined,
    /// before it's encoded (the <c>gml_default</c> attribute). It can use the wrapper's parameter names, which are the
    /// spec names with a leading underscore. Defaults are applied in parameter order, so an earlier parameter's
    /// default is already applied when a later one is computed. The spec's backtick value can't contain spaces. Null when the native
    /// side's own default applies.
    /// </param>
    public sealed record IrParameter(string Name, IrType Type, bool IsOptional, string? GmlDefault = null)
    {
        /// <summary>
        /// Creates a "self" parameter with the specified type.
        /// </summary>
        /// <param name="Type">The type of the self parameter.</param>
        /// <returns>A new IrParameter representing a self parameter.</returns>
        public static IrParameter Self(IrType Type) => new("self", Type, false);
    }

}
