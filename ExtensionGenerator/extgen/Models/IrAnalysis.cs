using codegencore.Models;
using extgen.Models.Utils;

namespace extgen.Models
{
    public static class IrAnalysis
    {
        // determine if we *could* pass everything directly respecting the 16 arg and ">4 implies all doubles" rule.
        private static bool DirectPassingFeasible(IrFunction fn, out bool hasString, out int directCount)
        {
            hasString = false;
            directCount = 0;
            foreach (var p in fn.Parameters)
            {
                if (p.Type.IsNullable()) return false;
                if (IrTypeUtil.IsStringScalar(p.Type)) { hasString = true; directCount++; continue; }
                if (IrTypeUtil.IsNumericScalar(p.Type) && !(p.Type is IrType.Builtin { Kind: BuiltinKind.Int64 or BuiltinKind.UInt64 })) { directCount++; continue; }
                // non‑directable
                return false;
            }

            if (NeedsRetBuffer(fn)) 
            {
                hasString = true;
                directCount+=2;
            }

            //  now validate meta‑rules
            if (directCount > 4 && hasString) return false;          // char* not allowed when >4 args
            if (directCount > 16) return false;          // engine hard‑limit
            return true;
        }

        public static bool NeedsArgsBuffer(IrFunction fn)
        {
            var feasible = DirectPassingFeasible(fn, out _, out _);
            return !feasible;
        }

        public static bool NeedsRetBuffer(IrFunction fn) {

            if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Void }) return false;

            if (IrTypeUtil.IsStringScalar(fn.ReturnType)) return false;
            if (IrTypeUtil.IsNumericScalar(fn.ReturnType) && !(fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Int64 or BuiltinKind.UInt64 })) return false;

            return true;
        }

        public static IEnumerable<IrParameter> DirectArgs(IrFunction fn)
        {
            if (!DirectPassingFeasible(fn, out _, out _))
                return [];
            return fn.Parameters;   // all are direct
        }
    }
}
