namespace extgen.Models.Utils
{
    /// <summary>
    /// Utilities for transforming IR function definitions.
    /// </summary>
    internal static class IrFunctionUtil
    {
        /// <summary>
        /// Transforms a struct method into a top-level function.
        /// Prefixes the name with the struct name and includes the self parameter.
        /// </summary>
        public static IrFunction PatchStructMethod(IrStruct s, IrFunction f) =>
            f with { Name = $"{s.Name}__{f.Name}", Parameters = f.FullParameters };
    }
}
