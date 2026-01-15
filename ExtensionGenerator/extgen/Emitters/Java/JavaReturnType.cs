using codegencore.Writers.Lang;
using extgen.Emitters.Utils;
using extgen.Model;

namespace extgen.Emitters.Java
{
    internal static class JavaReturnType
    {
        public static string ReturnFor(IrFunction f)
        {
            return ExportTypeUtils.ReturnFor(f).AsJavaType();
        }

        public static void EmitReturn(JavaWriter w, string resultVar, IrFunction f)
        {
            var t = f.ReturnType;
            if (t.IsStringScalar)
                w.Return(resultVar);
            else if (t.IsNumericScalar)
                if (t.Name == "bool")
                    w.Return($"{resultVar} ? 1.0 : 0.0");
                else
                    w.Return($"(double){resultVar}");
            else
                w.Return("0");
        }
    }
}
