namespace extgencore.Helpers
{
    /// <summary>
    /// Provides utilities for identifying and categorizing scalar types in the IR system.
    /// </summary>
    public static class ScalarTypes
    {
        private static readonly HashSet<string> _numeric = new(StringComparer.OrdinalIgnoreCase)
        { "bool", "uint8", "int8", "uint16", "int16", "uint32", "int32", "uint64", "float", "double" };
        private static readonly HashSet<string> _all = new(_numeric, StringComparer.OrdinalIgnoreCase)
        { "string" };

        /// <summary>
        /// Determines whether a type name is a recognized scalar type.
        /// </summary>
        public static bool IsKnown(string name) => _all.Contains(name);

        /// <summary>
        /// Determines whether a type name is a numeric scalar type.
        /// </summary>
        public static bool IsNumeric(string name) => _numeric.Contains(name);
    }
}
