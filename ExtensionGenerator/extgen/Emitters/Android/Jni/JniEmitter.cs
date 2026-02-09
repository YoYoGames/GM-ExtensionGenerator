using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Emitters.Utils;
using extgen.Models;
using extgen.Models.Config;
using extgen.Options.Android;
using extgen.Utils;
using System.Security.Cryptography;
using System.Text;

namespace extgen.Emitters.Android.Jni
{
    internal class JniEmitter(AndroidEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string dir)
        {  
            var ctx = new JniEmitterContext(comp.Name, settings, runtime);
            var specs = BuildSpecs(ctx, comp).ToArray();

            JniLayout layout = new(dir, settings);

            // --------------------------- Java Side ---------------------------

            FileEmitHelpers.WriteJava(layout.JavaCodeGenDir, $"{comp.Name}Internal.java", w => EmitInternal(ctx, comp, specs, w));
            FileEmitHelpers.WriteJava(layout.JavaCodeGenDir, $"{comp.Name}Bridge.java", w => EmitBridge(ctx, comp, specs, w));

            // If there is no implementation file generate one
            FileEmitHelpers.WriteJavaIfMissing(layout.JavaBaseDir, $"{comp.Name}.java", w => EmitImplementation(ctx, w));


            // --------------------------- C++ Side ----------------------------

            FileEmitHelpers.WriteCpp(layout.NativeCodeGenDir, $"{comp.Name}Internal_jni.cpp", w => EmitNative(ctx, comp, specs, w));
        }

        private static IEnumerable<JniFunctionSpec> BuildSpecs(JniEmitterContext ctx, IrCompilation comp)
        {
            foreach (var fn in comp.Functions)
            {
                yield return new JniFunctionSpec(
                    Name: fn.Name,
                    ExportName: $"{ctx.Runtime.JniPrefix}{fn.Name}",
                    NativeName: $"{ctx.Runtime.NativePrefix}{fn.Name}",
                    ExportParams: ExportTypeUtils.ParamsFor(fn, ctx.Runtime),
                    ExportReturnType: ExportTypeUtils.ReturnFor(fn)
                );
            }
        }

        // ------------------------------------------------------------------
        // Java: Implementation shell
        // ------------------------------------------------------------------

        private static void EmitImplementation(JniEmitterContext ctx, JavaWriter w) 
        {
            w.Package(ctx.Runtime.BasePackage);
            w.Import("java.lang.String");
            w.Import("java.nio.ByteBuffer");
            w.Line();

            w.Class($"{ctx.ExtName}", $"{ctx.ExtName}Internal", body => { }, modifiers: ["public", "final"], null);
        }

        // ------------------------------------------------------------------
        // Java: Internal (engine entry points -> static native bridge funcs)
        // ------------------------------------------------------------------

        private static void EmitInternal(JniEmitterContext ctx, IrCompilation c, IReadOnlyList<JniFunctionSpec> specs, JavaWriter w)
        {
            var pkg = ctx.Runtime.BasePackage;
            var wire = ctx.Runtime.WireClass;

            w.Package(pkg);
            w.Import($"static {ctx.Runtime.BridgePackage}.{ctx.BridgeClass}.*");
            w.Import("java.lang.String");
            w.Import("java.nio.ByteBuffer");
            w.Line();

            w.Class($"{ctx.ExtName}Internal", "RunnerSocial", body =>
            {
                var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
                var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer)));

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

                // function-level bridges calling static native exports on Bridge class
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

        // ------------------------------------------------------------------
        // Java: Bridge class (static native methods + register)
        // ------------------------------------------------------------------

        private static void EmitBridge(JniEmitterContext ctx, IrCompilation c, IReadOnlyList<JniFunctionSpec> specs, JavaWriter w)
        {
            w.Package(ctx.Runtime.BridgePackage);
            w.Import("java.lang.String");
            w.Import("java.nio.ByteBuffer");
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

                var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
                var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer)));

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

        // ------------------------------------------------------------------
        // C++: JNI side
        // ------------------------------------------------------------------

        private static void EmitNative(JniEmitterContext ctx, IrCompilation c, IReadOnlyList<JniFunctionSpec> specs, CppWriter w)
        {
            // Keep these in sync with GenJavaLayer
            var packageDot = ctx.Runtime.BridgePackage;
            var packageUnderscore = ctx.BridgePackageUnderscore;
            var bridgeClass = ctx.BridgeClass;

            EmitNativeHeader(ctx, w);
            EmitNativeInternals(ctx, c, w);

            // Build wrappers + register table
            var entries = new List<(string javaName, string jniSig, string cFun)>();

            foreach (var s in specs)
            {
                var sigArgs = string.Concat(s.ExportParams.Select(p => p.HostType.AsJniSig()));
                var sigRet = s.ExportReturnType.AsJniSig();

                var jniSig = $"({sigArgs}){sigRet}";

                // Stable unique C wrapper name
                var cWrap = $"{ctx.Runtime.JniWrapperPrefix}{s.Name}_{MangleSuffix(s.Name, jniSig)}";
                EmitNativeWrapper(w, bridgeClass, s, cWrap, jniSig);
                entries.Add((s.ExportName, jniSig, cWrap));
            }

            EmitNativeRegister(ctx, c, w, packageUnderscore, bridgeClass, entries);
        }

        private static void EmitNativeInternals(JniEmitterContext ctx, IrCompilation c, CppWriter w)
        {
            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
            var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer)));

            if (usesFunctions)
            {
                w.Lines($$"""
                // __{{ctx.ExtName}}_invocation_handler JNI wrapper signature: (Ljava/nio/ByteBuffer;D)D
                static jdouble {{ctx.Runtime.JniWrapperPrefix}}{{ctx.ExtName}}_invocation_handler(JNIEnv* env, jclass /* {{ctx.ExtName}}Bridge */, jobject __ret_buffer, jdouble __ret_buffer_length)
                {
                    void* __ret_buffer_ptr = env->GetDirectBufferAddress(__ret_buffer);
                    jlong __ret_buffer_cap = env->GetDirectBufferCapacity(__ret_buffer);
                    if (!__ret_buffer_ptr || __ret_buffer_cap <= 0) {
                        throwIAE(env, "__arg_buffer must be a DIRECT ByteBuffer");
                        return 0.0;
                    }
                    double __ret = {{ctx.Runtime.NativePrefix}}{{ctx.ExtName}}_invocation_handler((char *)__ret_buffer_ptr, static_cast<double>(__ret_buffer_length));
                    return static_cast<jdouble>(__ret);
                }
                """);
            }

            if (usesBuffers)
            {
                w.Lines($$"""
                // __{{ctx.ExtName}}_queue_buffer JNI wrapper signature: (Ljava/nio/ByteBuffer;D)D
                static jdouble {{ctx.Runtime.JniWrapperPrefix}}{{ctx.ExtName}}_queue_buffer(JNIEnv* env, jclass /* {{ctx.ExtName}}Bridge */, jobject __arg_buffer, jdouble __arg_buffer_length)
                {
                    void* __arg_buffer_ptr = env->GetDirectBufferAddress(__arg_buffer);
                    jlong __arg_buffer_cap = env->GetDirectBufferCapacity(__arg_buffer);
                    if (!__arg_buffer_ptr || __arg_buffer_cap <= 0) {
                        throwIAE(env, "__arg_buffer must be a DIRECT ByteBuffer");
                        return 0.0;
                    }

                    double __ret = {{ctx.Runtime.NativePrefix}}{{ctx.ExtName}}_queue_buffer((char *)__arg_buffer_ptr, static_cast<double>(__arg_buffer_length));
                    return static_cast<jdouble>(__ret);
                }
                """);
            }
        }

        private static void EmitNativeHeader(JniEmitterContext ctx, CppWriter w)
        {
            // File header
            w.Comment("AUTOGENERATED by your tool — DO NOT EDIT")
            .Include("jni.h")
            .Include("cstddef")
            .Include("algorithm")
            .Include("string")
            .Include($"native/{ctx.ExtName}Internal_native.h", false)
            .Line()
            .Lines($$"""
                // Per-library globals
                static JavaVM* g_vm = nullptr;

                extern "C" jint JNI_OnLoad(JavaVM* vm, void*) { g_vm = vm; return JNI_VERSION_1_6; }

                static void throwIAE(JNIEnv* env, const char* msg) {
                    jclass iae = env->FindClass("java/lang/IllegalArgumentException");
                    if (iae) env->ThrowNew(iae, msg);
                }

                // RAII pin/unpin for jstring UTF-8 chars
                struct UtfChars {
                    JNIEnv* env; jstring s; const char* p;
                    UtfChars(JNIEnv* e, jstring js) : env(e), s(js), p(js ? e->GetStringUTFChars(js, nullptr) : nullptr) {}
                    ~UtfChars() { if (s && p) env->ReleaseStringUTFChars(s, p); }
                    const char* c_str() const { return p; }
                };
                """)
            .Line();
        }

        private static void EmitNativeWrapper(CppWriter w, string bridgeClass, JniFunctionSpec s, string cWrap, string jniSig)
        {
            // JNI param list: (JNIEnv*, jclass /*Bridge*/) + per-arg
            var jniParams = new List<Param>
                {
                    new("JNIEnv*", FunctionRequiresEnvAccess(s) ? "env" : "/* env */"),
                    new("jclass",  $"/* {bridgeClass} */")
                };
            jniParams.AddRange(s.ExportParams.AsJniType());

            w.Comment($"{s.Name} JNI wrapper signature: {jniSig}");
            w.Function(cWrap, jniParams, funcBody =>
            {
                var callArgs = new List<string>();
                foreach (var p in s.ExportParams)
                {
                    var hostType = p.HostType;
                    switch (hostType)
                    {
                        case ExportType.Double:
                            callArgs.Add($"static_cast<double>({p.Name})");
                            break;
                        case ExportType.String:
                            var tmp = $"__pin_{p.Name}";
                            funcBody.Line($"UtfChars {tmp}(env, {p.Name});"); // Freed in the destructor
                            callArgs.Add($"(char *){tmp}.c_str()"); // implicit to const char*
                            break;
                        case ExportType.Pointer:
                            // arguments or return buffers
                            var tmp_ptr = $"{p.Name}_ptr";
                            var tmp_cap = $"{p.Name}_cap";
                            funcBody.Lines($$"""
                                void* {{tmp_ptr}} = env->GetDirectBufferAddress({{p.Name}});
                                jlong {{tmp_cap}} = env->GetDirectBufferCapacity({{p.Name}});
                                if (!{{tmp_ptr}} || {{tmp_cap}} <= 0) {
                                    throwIAE(env, "{{p.Name}} must be a DIRECT ByteBuffer");
                                    return {{(s.ExportReturnType == ExportType.String ? "nullptr" : "0.0")}};
                                }
                                """);
                            callArgs.Add($"(char *){tmp_ptr}"); // explicit to char*
                            // The next argument in the loop will be the size (protocol)
                            break;
                    }
                }

                // ---- Call real implementation ----
                switch (s.ExportReturnType)
                {
                    case ExportType.String:
                        funcBody.Line($"const char* __out = {s.NativeName}({string.Join(", ", callArgs)});");
                        funcBody.Line("jstring __j = __out ? env->NewStringUTF(__out) : nullptr;");
                        funcBody.Line("return __j;");
                        break;
                    case ExportType.Double:
                        funcBody.Line($"double __ret = {s.NativeName}({string.Join(", ", callArgs)});");
                        funcBody.Line("return static_cast<jdouble>(__ret);");
                        break;
                    default:
                        throw new NotImplementedException("Return types can only be strings or doubles");
                }

            }, s.ExportReturnType.AsJniType(), modifiers: ["static"])
            .Line();
        }

        private static bool FunctionRequiresEnvAccess(JniFunctionSpec functionSpec) => 
            functionSpec.ExportParams.Any(ep => ep.HostType == ExportType.String || ep.HostType == ExportType.Pointer) || 
            functionSpec.ExportReturnType == ExportType.String;

        private static void EmitNativeRegister(JniEmitterContext ctx, IrCompilation c, CppWriter w, string packageUnderscore, string bridgeClass, List<(string javaName, string jniSig, string cFun)> entries)
        {
            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
            var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer) ));

            // ---- nativeRegister + RegisterNatives (unchanged logic, cleaner input) ----
            var nativeInitSymbol = $"Java_{packageUnderscore}_{bridgeClass}_nativeRegister";
            w.Comment("nativeRegister(Class callbackClass): cache callback + register all JNI wrappers");
            w.Extern(CppLinkage.C, ex =>
            {
                ex.Function(nativeInitSymbol,
                    [ new Param("JNIEnv*", "env"),
                      new Param("jclass",  "bridgeClass") ],
                    body =>
                    {
                        body.Comment("Registers all the native methods")
                            .Assign("methods[]", b => b.Block(block =>
                            {
                                if (usesFunctions)
                                {
                                    block.Line($"{{ \"{ctx.Runtime.JniPrefix}{ctx.ExtName}_invocation_handler\", \"(Ljava/nio/ByteBuffer;D)D\", (void*){ctx.Runtime.JniWrapperPrefix}{ctx.ExtName}_invocation_handler }},");
                                }

                                if (usesBuffers)
                                {
                                    block.Line($"{{ \"{ctx.Runtime.JniPrefix}{ctx.ExtName}_queue_buffer\", \"(Ljava/nio/ByteBuffer;D)D\", (void*){ctx.Runtime.JniWrapperPrefix}{ctx.ExtName}_queue_buffer }},");
                                }

                                foreach (var e in entries)
                                    block.Line($"{{ \"{e.javaName}\", \"{e.jniSig}\", (void*){e.cFun} }},");
                            }), "static const JNINativeMethod")
                            .Line()
                            .Call("env->RegisterNatives", ["bridgeClass", "methods", "sizeof(methods)/sizeof(methods[0])"]).Line(";");
                    },
                    "void JNICALL",
                    modifiers: ["JNIEXPORT"]
                );
            });
            w.Line();
        }

        // ------------------------------------------------------------------
        // Utils
        // ------------------------------------------------------------------

        private static string MangleSuffix(string name, string sig)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name + "|" + sig));
            return Convert.ToHexString(bytes, 0, 6); // 6 hex chars is plenty
        }

    }
}
