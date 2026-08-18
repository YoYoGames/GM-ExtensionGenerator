using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Emitters.Rust;
using System.Text;

namespace extgen.Emitters.Android.Jni
{
    /// <summary>
    /// Emits rust/src/generated/android_jni.rs — JNI entrypoints that forward to __EXT_NATIVE__*.
    /// </summary>
    internal sealed class RustJniBridgeEmitter(RuntimeNaming runtime) : IJniNativeBridgeEmitter
    {
        public void EmitBridge(
            JniEmitterContext ctx,
            IrCompilation comp,
            IReadOnlyList<JniFunctionSpec> specs,
            JniLayout layout)
        {
            var genDir = Path.Combine(layout.OutputRoot, "rust", "src", "generated");
            Directory.CreateDirectory(genDir);

            var text = Generate(ctx, comp, specs);
            File.WriteAllText(Path.Combine(genDir, "android_jni.rs"), text, new UTF8Encoding(false));
        }

        private string Generate(JniEmitterContext ctx, IrCompilation comp, IReadOnlyList<JniFunctionSpec> specs)
        {
            var usesFunctions = comp.HasFunctionType();
            var usesBuffers = comp.HasBufferType();

            var sb = new StringBuilder();
            sb.AppendLine("// ##### extgen :: Auto-generated Android JNI bridge (Rust) #####");
            sb.AppendLine("#![allow(non_snake_case)]");
            sb.AppendLine();
            sb.AppendLine("use jni::objects::{JByteBuffer, JClass, JObject, JString, JValue};");
            sb.AppendLine("use jni::sys::{jdouble, jint, jstring, JNI_VERSION_1_6};");
            sb.AppendLine("use jni::{JNIEnv, JavaVM, NativeMethod};");
            sb.AppendLine("use std::ffi::c_void;");
            sb.AppendLine("use std::os::raw::c_char;");
            sb.AppendLine();
            sb.AppendLine("use crate::generated::ffi;");
            sb.AppendLine();

            var packageUnderscore = ctx.BridgePackageUnderscore;
            var bridgeClass = ctx.BridgeClass;
            var nativeRegister = $"Java_{packageUnderscore}_{bridgeClass}_nativeRegister";

            // Helper to get direct buffer ptr
            sb.AppendLine("fn direct_buf_ptr(env: &mut JNIEnv<'_>, buf: JObject<'_>) -> Option<*mut c_char> {");
            sb.AppendLine("    let bb = unsafe { JByteBuffer::from_raw(buf.as_raw()) };");
            sb.AppendLine("    env.get_direct_buffer_address(&bb).ok().map(|p| p as *mut c_char)");
            sb.AppendLine("}");
            sb.AppendLine();

            if (usesFunctions)
            {
                EmitSyntheticBufferLenWrapper(
                    sb,
                    wrapName: "jni_wrap_invocation_handler",
                    nativeName: $"{ctx.Runtime.NativePrefix}{ctx.ExtName}_invocation_handler",
                    bufferParam: ctx.Runtime.RetBufferParam,
                    lengthParam: ctx.Runtime.RetBufferLengthParam);
            }

            if (usesBuffers)
            {
                EmitSyntheticBufferLenWrapper(
                    sb,
                    wrapName: "jni_wrap_queue_buffer",
                    nativeName: $"{ctx.Runtime.NativePrefix}{ctx.ExtName}_queue_buffer",
                    bufferParam: ctx.Runtime.ArgBufferParam,
                    lengthParam: ctx.Runtime.ArgBufferLengthParam);
            }

            foreach (var s in specs)
            {
                EmitWrapper(sb, ctx, s);
            }

            // nativeRegister + RegisterNatives
            sb.AppendLine("#[no_mangle]");
            sb.AppendLine($"pub extern \"system\" fn {nativeRegister}(mut env: JNIEnv<'_>, class: JClass<'_>) {{");
            sb.AppendLine("    let methods = [");
            if (usesFunctions)
            {
                sb.AppendLine(
                    $"        NativeMethod {{ name: \"{ctx.Runtime.JniPrefix}{ctx.ExtName}_invocation_handler\".into(), sig: \"(Ljava/nio/ByteBuffer;D)D\".into(), fn_ptr: jni_wrap_invocation_handler as *mut c_void }},");
            }
            if (usesBuffers)
            {
                sb.AppendLine(
                    $"        NativeMethod {{ name: \"{ctx.Runtime.JniPrefix}{ctx.ExtName}_queue_buffer\".into(), sig: \"(Ljava/nio/ByteBuffer;D)D\".into(), fn_ptr: jni_wrap_queue_buffer as *mut c_void }},");
            }
            foreach (var s in specs)
            {
                var sigArgs = string.Concat(s.ExportParams.Select(p => p.HostType.AsJniSig()));
                var sigRet = s.ExportReturnType.AsJniSig();
                var jniSig = $"({sigArgs}){sigRet}";
                var wrap = WrapperName(ctx, s);
                sb.AppendLine($"        NativeMethod {{ name: \"{s.ExportName}\".into(), sig: \"{jniSig}\".into(), fn_ptr: {wrap} as *mut c_void }},");
            }
            sb.AppendLine("    ];");
            sb.AppendLine("    let _ = env.register_native_methods(class, &methods);");
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("#[no_mangle]");
            sb.AppendLine("pub extern \"system\" fn JNI_OnLoad(_vm: JavaVM, _reserved: *mut c_void) -> jint {");
            sb.AppendLine("    JNI_VERSION_1_6");
            sb.AppendLine("}");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// JNI wrapper for synthetic helpers: (ByteBuffer, double length) -> double.
        /// Used by queue_buffer and invocation_handler (same shape as CppJniBridgeEmitter).
        /// </summary>
        private static void EmitSyntheticBufferLenWrapper(
            StringBuilder sb,
            string wrapName,
            string nativeName,
            string bufferParam,
            string lengthParam)
        {
            sb.AppendLine($"extern \"system\" fn {wrapName}(");
            sb.AppendLine("    mut env: JNIEnv<'_>,");
            sb.AppendLine("    _class: JClass<'_>,");
            sb.AppendLine($"    {bufferParam}: JObject<'_>,");
            sb.AppendLine($"    {lengthParam}: jdouble,");
            sb.AppendLine(") -> jdouble {");
            sb.AppendLine(
                $"    let {bufferParam}_ptr = match direct_buf_ptr(&mut env, {bufferParam}) {{ Some(p) => p, None => return -1.0 }};");
            sb.AppendLine(
                $"    unsafe {{ ffi::{nativeName}({bufferParam}_ptr, {lengthParam}) }}");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static string WrapperName(JniEmitterContext ctx, JniFunctionSpec s) =>
            $"jni_wrap_{RustCodeGen.SanitizeIdent(s.Name)}";

        private static void EmitWrapper(StringBuilder sb, JniEmitterContext ctx, JniFunctionSpec s)
        {
            var wrap = WrapperName(ctx, s);
            var needsEnv = s.ExportParams.Any(p => p.HostType is ExportType.String or ExportType.Pointer)
                           || s.ExportReturnType == ExportType.String;

            sb.AppendLine("extern \"system\" fn " + wrap + "(");
            sb.AppendLine(needsEnv ? "    mut env: JNIEnv<'_>," : "    _env: JNIEnv<'_>,");
            sb.AppendLine("    _class: JClass<'_>,");

            var paramDecls = new List<string>();
            foreach (var p in s.ExportParams)
            {
                var id = RustCodeGen.SanitizeIdent(p.Name);
                paramDecls.Add(p.HostType switch
                {
                    ExportType.Double => $"    {id}: jdouble",
                    ExportType.String => $"    {id}: JString<'_>",
                    ExportType.Pointer => $"    {id}: JObject<'_>",
                    _ => $"    {id}: jdouble"
                });
            }
            sb.AppendLine(string.Join(",\n", paramDecls));

            var retTy = s.ExportReturnType switch
            {
                ExportType.String => "jstring",
                _ => "jdouble"
            };
            sb.AppendLine($") -> {retTy} {{");

            var callArgs = new List<string>();
            foreach (var p in s.ExportParams)
            {
                var id = RustCodeGen.SanitizeIdent(p.Name);
                switch (p.HostType)
                {
                    case ExportType.Double:
                        callArgs.Add(id);
                        break;
                    case ExportType.String:
                        sb.AppendLine($"    let {id}_c = env.get_string(&{id}).ok();");
                        sb.AppendLine($"    let {id}_ptr = {id}_c.as_ref().map(|s| s.as_ptr()).unwrap_or(std::ptr::null());");
                        callArgs.Add($"{id}_ptr");
                        break;
                    case ExportType.Pointer:
                        sb.AppendLine($"    let {id}_ptr = match direct_buf_ptr(&mut env, {id}) {{ Some(p) => p, None => return {(s.ExportReturnType == ExportType.String ? "std::ptr::null_mut()" : "-1.0")} }};");
                        callArgs.Add($"{id}_ptr");
                        break;
                }
            }

            sb.AppendLine($"    let result = unsafe {{ ffi::{s.NativeName}({string.Join(", ", callArgs)}) }};");
            if (s.ExportReturnType == ExportType.String)
            {
                sb.AppendLine("    if result.is_null() { return std::ptr::null_mut(); }");
                sb.AppendLine("    let cstr = unsafe { std::ffi::CStr::from_ptr(result) };");
                sb.AppendLine("    match cstr.to_str() {");
                sb.AppendLine("        Ok(s) => env.new_string(s).map(|js| js.into_raw()).unwrap_or(std::ptr::null_mut()),");
                sb.AppendLine("        Err(_) => std::ptr::null_mut(),");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine("    result");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }
    }
}
