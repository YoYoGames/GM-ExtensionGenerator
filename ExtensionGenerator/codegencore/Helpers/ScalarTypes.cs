// -----------------------------------------------------------------------------
//  Code‑Generation Intermediate Representation (IR)
//  Full reference implementation with emitters that optimise buffer usage:
//   • If a function has *no* parameters, the wrappers omit the args buffer.
//   • If **all** parameters are doubles or strings, they’re passed directly
//     (each as a <c>double</c> or <c>char*</c>) – no packing/unpacking cost.
//   • If the return value maps to a double, no return buffer is used;
//     otherwise a return‑buffer pair is appended to the call.
// -----------------------------------------------------------------------------

namespace extgencore.Helpers
{
    public static class ScalarTypes
    {
        private static readonly HashSet<string> _numeric = new(StringComparer.OrdinalIgnoreCase)
        { "bool", "uint8", "int8", "uint16", "int16", "uint32", "int32", "uint64", "float", "double" };
        private static readonly HashSet<string> _all = new(_numeric, StringComparer.OrdinalIgnoreCase)
        { "string" };

        public static bool IsKnown(string name) => _all.Contains(name);
        public static bool IsNumeric(string name) => _numeric.Contains(name);
    }
}
