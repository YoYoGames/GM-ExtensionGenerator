using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Options.Android;
using extgen.Utils;

namespace extgen.Emitters.Android.Jni
{
    /// <summary>
    /// Emits Java Internal/Bridge/impl classes for JNI mode (shared by Cpp and Rust native bridges).
    /// </summary>
    internal static class JniJavaLayer
    {
        public static void Emit(
            JniEmitterContext ctx,
            IrCompilation c,
            IReadOnlyList<JniFunctionSpec> specs,
            JniLayout layout)
        {
            FileEmitHelpers.WriteJava(layout.JavaCodeGenDir, $"{c.Name}Internal.java", w => EmitInternal(ctx, c, specs, w));
            FileEmitHelpers.WriteJava(layout.JavaCodeGenDir, $"{c.Name}Bridge.java", w => EmitBridge(ctx, c, specs, w));
            FileEmitHelpers.WriteJavaIfMissing(layout.JavaBaseDir, $"{c.Name}.java", w => EmitImplementation(ctx, w));
        }

        private static void EmitImplementation(JniEmitterContext ctx, JavaWriter w)
        {
            w.Package(ctx.Runtime.BasePackage);
            w.Import("java.lang.String");
            w.Import("java.nio.ByteBuffer");
            w.Line();

            w.Class($"{ctx.ExtName}", $"{ctx.ExtName}Internal", body => { }, modifiers: ["public", "final"], null);
        }

        private static void EmitInternal(JniEmitterContext ctx, IrCompilation c, IReadOnlyList<JniFunctionSpec> specs, JavaWriter w)
        {
            var pkg = ctx.Runtime.BasePackage;

            w.Package(pkg);
            w.Import($"static {ctx.Runtime.BridgePackage}.{ctx.BridgeClass}.*");
            w.Import("java.lang.String");
            w.Import("java.nio.ByteBuffer");
            w.Line();

            w.Class($"{ctx.ExtName}Internal", "RunnerSocial", body =>
            {
                var usesFunctions = c.HasFunctionType();
                var usesBuffers = c.HasBufferType();

                if (usesFunctions)
                {
                    body.Function(
                        name: $"{ctx.Runtime.NativePrefix}{ctx.ExtName}_invocation_handler",
                        parameters: [
                            new Param("ByteBuffer", ctx.Runtime.RetBufferParam),
                            new Param("double", ctx.Runtime.RetBufferLengthParam)
                        ],
                        body: m => m.Return(expr => expr.Call($"{ctx.Runtime.JniPrefix}{ctx.ExtName}_invocation_handler", ctx.Runtime.RetBufferParam, ctx.Runtime.RetBufferLengthParam)),
                        returnType: "double",
                        modifiers: ["public"]
                    );
                }

                if (usesBuffers)
                {
                    body.Function(
                        name: $"{ctx.Runtime.NativePrefix}{ctx.ExtName}_queue_buffer",
                        parameters: [
                            new Param("ByteBuffer", ctx.Runtime.ArgBufferParam),
                            new Param("double", ctx.Runtime.ArgBufferLengthParam)
                        ],
                        body: m => m.Return(expr => expr.Call($"{ctx.Runtime.JniPrefix}{ctx.ExtName}_queue_buffer", ctx.Runtime.ArgBufferParam, ctx.Runtime.ArgBufferLengthParam)),
                        returnType: "double",
                        modifiers: ["public"]
                    );
                }

                foreach (var s in specs)
                {
                    body.Function(
                        s.NativeName,
                        s.ExportParams.AsJava(),
                        funcBody => funcBody.Return(expr => expr.Call(s.ExportName, [.. s.ExportParams.Select(p => p.Name)])),
                        s.ExportReturnType.AsJavaType(),
                        modifiers: ["public"]
                    );
                }
            }, modifiers: ["public"], null);
        }

        private static void EmitBridge(JniEmitterContext ctx, IrCompilation c, IReadOnlyList<JniFunctionSpec> specs, JavaWriter w)
        {
            w.Package(ctx.Runtime.BridgePackage);
            w.Import("java.lang.String");
            w.Import("java.nio.ByteBuffer");
            w.Import("${YYAndroidPackageName}.GMExtUtils");
            w.Line();

            w.Class($"{ctx.ExtName}Bridge", body =>
            {
                body.ModBlock(["static"], staticBlock =>
                {
                    staticBlock
                        .Comment("this is the extension lib name")
                        .Call("System.loadLibrary", $"\"{ctx.LibraryName}\"").Line(";")
                        .Call("nativeRegister", []).Line(";");
                });
                body.Line();

                body.Comment("this registers the native functions on the C++ layer");
                body.FunctionDecl("nativeRegister", [], modifiers: ["private", "static", "native"]);
                body.Line();

                var usesFunctions = c.HasFunctionType();
                var usesBuffers = c.HasBufferType();

                body.Function("__EXT_JAVA__GetExtensionOption", parameters: [
                        new Param("String", "extName"),
                        new Param("String", "optName")
                    ], m => m.Return(expr => expr.Call("GMExtUtils.GetExtensionOption", "extName", "optName")),
                    "String", modifiers: ["public", "static"]);
                body.Line();

                if (usesFunctions)
                {
                    body.FunctionDecl($"{ctx.Runtime.JniPrefix}{ctx.ExtName}_invocation_handler", parameters: [
                            new Param("ByteBuffer", ctx.Runtime.RetBufferParam),
                            new Param("double", ctx.Runtime.RetBufferLengthParam)
                        ], "double", modifiers: ["public", "static", "native"]);
                }

                if (usesBuffers)
                {
                    body.FunctionDecl($"{ctx.Runtime.JniPrefix}{ctx.ExtName}_queue_buffer", parameters: [
                        new Param("ByteBuffer", ctx.Runtime.ArgBufferParam),
                        new Param("double", ctx.Runtime.ArgBufferLengthParam)
                    ], "double", modifiers: ["public", "static", "native"]);
                }

                foreach (var s in specs)
                {
                    body.FunctionDecl(
                        s.ExportName,
                        s.ExportParams.AsJava(),
                        s.ExportReturnType.AsJavaType(),
                        modifiers: ["public", "static", "native"]
                    );
                }

            }, ["public", "final"]);
        }
    }
}
