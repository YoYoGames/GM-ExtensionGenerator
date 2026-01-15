namespace extgen.Model
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
                if (p.Type.IsNullable) return false;
                if (p.Type.IsNumericScalar) { directCount++; continue; }
                if (p.Type.IsStringScalar) { hasString = true; directCount++; continue; }
                // non‑directable
                return false;
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

        public static bool NeedsRetBuffer(IrFunction fn) => fn.ReturnType.Kind switch
            {
                IrTypeKind.Void => false,
                IrTypeKind.Scalar => !(fn.ReturnType.IsNumericScalar || fn.ReturnType.IsStringScalar),   // numeric → double
                _ => true                              // everything else needs a buffer
            };

        public static IEnumerable<IrParameter> DirectArgs(IrFunction fn)
        {
            if (!DirectPassingFeasible(fn, out _, out _))
                return [];
            return fn.Parameters;   // all are direct
        }
    }
}
