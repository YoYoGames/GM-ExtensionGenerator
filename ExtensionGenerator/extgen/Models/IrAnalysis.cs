using codegencore.Models;
using extgen.Models.Utils;

namespace extgen.Models
{
    /// <summary>
    /// Analyzes IR functions to determine parameter passing strategy.
    /// Implements GameMaker runtime constraints (16 arg limit, string restrictions, etc.).
    /// </summary>
    public static class IrAnalysis
    {
        /// <summary>
        /// Determines if direct parameter passing is feasible for a function.
        /// Respects GameMaker constraints: 16 arg limit, no strings when more than 4 args.
        /// </summary>
        private static bool DirectPassingFeasible(IrFunction fn, out bool hasString, out int directCount)
        {
            // GameMaker extension functions have hard limits imposed by the runtime's calling convention:
            // - Maximum 16 arguments total (engine limitation)
            // - If >4 args AND contains any string type, direct passing fails
            // - Nullable types can't be passed directly (no native optional representation)
            // - 64-bit integers need special handling (GML only has doubles, so we serialize them)
            //
            // When these constraints are violated, we fall back to "buffer protocol":
            // pack all args into a ByteBuffer, pass the buffer pointer + length as 2 args,
            // then unpack on the native side. This is slower but handles arbitrary complexity.

            hasString = false;
            directCount = 0;
            foreach (var p in fn.Parameters)
            {
                // Nullable types require buffer protocol (no way to pass "null" marker directly)
                if (p.Type.IsNullable())
                    return false;

                if (p.Type.IsStringScalar())
                {
                    hasString = true;
                    directCount++;
                    continue;
                }

                // Numeric scalars can be passed directly, EXCEPT int64/uint64 which exceed
                // GML's number precision (GML uses doubles internally, can't represent all int64 values)
                if (p.Type.IsNumericScalar() && !(p.Type is IrType.Builtin { Kind: BuiltinKind.Int64 or BuiltinKind.UInt64 }))
                {
                    directCount++;
                    continue;
                }

                // Any other type (arrays, structs, buffers, etc.) requires buffer protocol
                return false;
            }

            // If the return value needs a buffer, we add 2 extra params (ret buffer ptr + length).
            // This counts toward teh 16-arg limit and is treated as a "string-like" constraint.
            if (NeedsRetBuffer(fn))
            {
                hasString = true;  // Return buffer behaves like a string param for constraint checking
                directCount += 2;  // Pointer + length = 2 slots
            }

            // Core constraint: >4 direct args + any string type → must use buffer protocol
            // (This is a GameMaker quirk; strings interact poorly with variadic native calls)
            if (directCount > 4 && hasString)
                return false;

            // Absolute maximum enforced by GameMaker runtime
            if (directCount > 16)
                return false;

            return true;
        }

        /// <summary>
        /// Determines if a function requires buffer-based argument passing.
        /// </summary>
        public static bool NeedsArgsBuffer(IrFunction fn)
        {
            var feasible = DirectPassingFeasible(fn, out _, out _);
            return !feasible;
        }

        /// <summary>
        /// Determines if a function requires buffer-based return value passing.
        /// </summary>
        public static bool NeedsRetBuffer(IrFunction fn)
        {
            if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Void })
                return false;

            if (fn.ReturnType.IsStringScalar())
                return false;

            if (fn.ReturnType.IsNumericScalar() && !(fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Int64 or BuiltinKind.UInt64 }))
                return false;

            return true;
        }

        /// <summary>
        /// Gets the parameters that can be passed directly (not via buffer).
        /// Returns empty if buffer passing is required.
        /// </summary>
        public static IEnumerable<IrParameter> DirectArgs(IrFunction fn)
        {
            if (!DirectPassingFeasible(fn, out _, out _))
                return [];

            return fn.Parameters;
        }
    }
}
