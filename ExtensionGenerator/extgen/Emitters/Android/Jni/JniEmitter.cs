using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.Options.Android;
using extgen.Utils;
using System.Security.Cryptography;
using System.Text;

namespace extgen.Emitters.Android.Jni
{
    /// <summary>
    /// Generates JNI bridge code for Android platform integration.
    /// </summary>
    internal class JniEmitter(AndroidEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        /// <summary>
        /// Emits JNI bridge code including Java internal/bridge classes and C++ JNI implementations.
        /// </summary>
        public void Emit(IrCompilation comp, string dir)
        {  
            var ctx = new JniEmitterContext(comp.Name, settings, runtime);
            var specs = BuildSpecs(ctx, comp).ToArray();

            JniLayout layout = new(dir, settings);

            FileEmitHelpers.WriteJava(layout.JavaCodeGenDir, $"{comp.Name}Internal.java", w => EmitInternal(ctx, comp, specs, w));
            FileEmitHelpers.WriteJava(layout.JavaCodeGenDir, $"{comp.Name}Bridge.java", w => EmitBridge(ctx, comp, specs, w));

            // If there is no implementation file generate one
            FileEmitHelpers.WriteJavaIfMissing(layout.JavaBaseDir, $"{comp.Name}.java", w => EmitImplementation(ctx, w));

            FileEmitHelpers.WriteCpp(layout.NativeCodeGenDir, $"{comp.Name}Internal_jni.cpp", w => EmitNative(ctx, comp, specs, w));
        }

        private static IEnumerable<JniFunctionSpec> BuildSpecs(JniEmitterContext ctx, IrCompilation c)
        {
            var allFunctions = c.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
            foreach (var fn in allFunctions)
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
            var wire = ctx.Runtime.WireClass;

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
                    "String", ["public", "static"]);
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
            var usesFunctions = c.HasFunctionType();
            var usesBuffers = c.HasBufferType();

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
            w.Comment("AUTOGENERATED - DO NOT EDIT")
            .Include("jni.h")
            .Include("cstddef")
            .Include("algorithm")
            .Include("string")
            .Include($"native/{ctx.ExtName}Internal_exports.h", false)
            .Line()
            .Lines($$"""
                // JNI requires global state because the VM reference must be captured at load time.
                // JNI_OnLoad() is called when the shared library is loaded; we save the JavaVM pointer
                // so that native threads can attach themselves later (see ScopedEnv below).
                static JavaVM* g_vm = nullptr;

                // Cached Java class/method references for callbacks into GML.
                // These are resolved once at init time and reused (JNI method lookups are expensive).
                static jclass g_bridgeClass = nullptr;
                static jmethodID g_mid_GetExtensionOption = nullptr;

                // JNI_OnLoad is called automatically when System.loadLibrary() occurs.
                // This is our one chance to capture the VM pointer before any other JNI calls.
                extern "C" jint JNI_OnLoad(JavaVM* vm, void*) { g_vm = vm; return JNI_VERSION_1_6; }

                static void throwIAE(JNIEnv* env, const char* msg) {
                    jclass iae = env->FindClass("java/lang/IllegalArgumentException");
                    if (iae) env->ThrowNew(iae, msg);
                }

                // ScopedEnv: RAII wrapper for JNIEnv* that handles thread attachment.
                // Native threads that weren't created by the JVM must call AttachCurrentThread
                // before making any JNI calls. This struct does it automatically in ctor,
                // and DetachCurrentThread in dtor. Main thread doesn't need attach (GetEnv succeeds).
                struct ScopedEnv {
                    JNIEnv* env = nullptr;
                    bool detach = false;

                    ScopedEnv() {
                        if (!g_vm) return;
                        jint rc = g_vm->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6);
                        if (rc == JNI_OK) return;  // Already attached (main thread case)
                        if (rc == JNI_EDETACHED && g_vm->AttachCurrentThread(&env, nullptr) == JNI_OK) {
                            detach = true;  // We attached, so we must detach in dtor
                        } else {
                            env = nullptr;  // Failed to attach
                        }
                    }

                    ~ScopedEnv() {
                        if (detach) g_vm->DetachCurrentThread();
                    }
                };

                // UtfChars: RAII wrapper for JNI string pinning.
                // GetStringUTFChars returns a temporary C-string pointer that MUST be released
                // via ReleaseStringUTFChars when done. This struct ensures proper cleanup.
                // Without this, we'd leak JNI local refs and potentially crash on ref table overflow.
                struct UtfChars {
                    JNIEnv* env; jstring s; const char* p;
                    UtfChars(JNIEnv* e, jstring js) : env(e), s(js), p(js ? e->GetStringUTFChars(js, nullptr) : nullptr) {}
                    ~UtfChars() { if (s && p) env->ReleaseStringUTFChars(s, p); }
                    const char* c_str() const { return p; }
                };
                """)
            .Line();

            EmitNativeJavaCallbacks(ctx, w);
            EmitNativeRunnerInit(ctx, w);
        }


        private static void EmitNativeJavaCallbacks(JniEmitterContext ctx, CppWriter w)
        {
            w.Lines("""
                static const char* __JNI_JAVA__GetExtensionOption(const char* extName, const char* optName)
                {
                    thread_local std::string tls_result;
                    tls_result.clear();

                    ScopedEnv scoped;
                    if (!scoped || !g_bridgeClass || !g_mid_GetExtensionOption) {
                        return nullptr;
                    }

                    JNIEnv* env = scoped.env;

                    jstring jExtName = extName ? env->NewStringUTF(extName) : nullptr;
                    jstring jOptName = optName ? env->NewStringUTF(optName) : nullptr;

                    jstring jRet = static_cast<jstring>(
                        env->CallStaticObjectMethod(g_bridgeClass, g_mid_GetExtensionOption, jExtName, jOptName)
                    );

                    if (jExtName) env->DeleteLocalRef(jExtName);
                    if (jOptName) env->DeleteLocalRef(jOptName);

                    if (env->ExceptionCheck()) {
                        env->ExceptionDescribe();
                        env->ExceptionClear();
                        if (jRet) env->DeleteLocalRef(jRet);
                        return nullptr;
                    }

                    if (!jRet) {
                        return nullptr;
                    }

                    const char* chars = env->GetStringUTFChars(jRet, nullptr);
                    if (!chars) {
                        env->DeleteLocalRef(jRet);
                        return nullptr;
                    }

                    tls_result.assign(chars);

                    env->ReleaseStringUTFChars(jRet, chars);
                    env->DeleteLocalRef(jRet);

                    return tls_result.c_str();
                }
                """);
        }

        private static void EmitNativeRunnerInit(JniEmitterContext ctx, CppWriter w)
        {
            w.Lines("""
                static void __JNI_InitExtUtils()
                {
                    gm::details::GMRTRunnerInterface runner{};
                    runner.ExtOptGetString = __JNI_JAVA__GetExtensionOption;
                    ExtUtils.Init(runner);
                }
                """);
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
                            // ByteBuffer marshaling for complex arguement types.
                            // CRITICAL: Must be a DIRECT buffer (off-heap memory), not a heap buffer.
                            // Direct buffers have stable memory addresses that survive JNI calls.
                            // Heap buffers would require copying the entire buffer on each access (slow + unsafe).
                            // GetDirectBufferAddress returns null for non-direct buffers; we throw if that occured.
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
                            callArgs.Add($"(char *){tmp_ptr}");
                            // Next loop iteration will handle the length param (buffers always come in ptr+length pairs)
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
                        body.Lines("""
                            if (!g_bridgeClass) {
                                g_bridgeClass = static_cast<jclass>(env->NewGlobalRef(bridgeClass));
                            }

                            if (!g_mid_GetExtensionOption) {
                                g_mid_GetExtensionOption = env->GetStaticMethodID(
                                    g_bridgeClass,
                                    "__EXT_JAVA__GetExtensionOption",
                                    "(Ljava/lang/String;Ljava/lang/String;)Ljava/lang/String;"
                                );
                                if (!g_mid_GetExtensionOption) {
                                    throwIAE(env, "Failed to bind __EXT_JAVA__GetExtensionOption");
                                    return;
                                }
                            }

                            static bool g_extUtilsInitialized = false;
                            if (!g_extUtilsInitialized) {
                                __JNI_InitExtUtils();
                                g_extUtilsInitialized = true;
                            }

                            """);

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

        private static string MangleSuffix(string name, string sig)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name + "|" + sig));
            return Convert.ToHexString(bytes, 0, 6); // 6 hex chars is plenty
        }

    }
}
