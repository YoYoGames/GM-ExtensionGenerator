using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Emitters.Utils;
using extgen.Models;

namespace extgen.Emitters.Android.Java
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

            // Nullable return is considered "complex" -> fallback always
            if (IrType.IsNullable(t))
            {
                w.Return("0");
                return;
            }

            // From here, treat it as non-nullable
            if (t is IrType.Builtin { Kind: BuiltinKind.String })
            {
                w.Return(resultVar);
                return;
            }

            if (t is IrType.Builtin { Kind: BuiltinKind.Bool })
            {
                w.Return($"{resultVar} ? 1.0 : 0.0");
                return;
            }

            if (t is IrType.Builtin b && b.Kind is
                BuiltinKind.Int8 or BuiltinKind.UInt8 or
                BuiltinKind.Int16 or BuiltinKind.UInt16 or
                BuiltinKind.Int32 or BuiltinKind.UInt32 or
                BuiltinKind.Int64 or BuiltinKind.UInt64 or
                BuiltinKind.Float32 or BuiltinKind.Float64)
            {
                w.Return($"(double){resultVar}");
                return;
            }

            // Everything else (struct/enum/array/any/buffer/function/etc.)
            w.Return("0");
        }
    }
}
