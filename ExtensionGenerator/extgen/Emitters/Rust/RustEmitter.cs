using codegencore.Models;
using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.Utils;
using System.Text;

namespace extgen.Emitters.Rust
{
    /// <summary>
    /// Emits portable Rust FFI (`__EXT_NATIVE__*`), IDL types/codecs, and user stubs.
    /// Mirrors C++ policy: generated/ always overwrite; user surface IfMissing.
    /// </summary>
    internal sealed class RustEmitter(RustEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            var ctx = new RustEmitterContext(comp.Name, settings, runtime);
            var layout = new RustLayout(outputDir);
            var enums = new IrTypeEnumResolver(comp.Enums);
            var typeMap = new RustTypeMap(enums);
            var wireForCodecs = new RustWireHelpers(typeMap, enums, runtime, structCodecPrefix: "");
            var wireForFfi = new RustWireHelpers(typeMap, enums, runtime, structCodecPrefix: "codecs::");
            var functions = comp.GetAllFunctions(IrFunctionUtil.PatchStructMethod).ToList();
            var specs = functions.Select(fn => NativeExportSpec.From(fn, runtime)).ToList();

            EmitWireCrate(layout);
            EmitTypes(layout, comp, typeMap);
            EmitCodecs(layout, comp, wireForCodecs);
            EmitFfi(layout, ctx, comp, functions, specs, wireForFfi);
            EmitLibRs(layout, ctx);
            EmitUserSurface(layout, functions, specs, typeMap);
        }

        private static void EmitWireCrate(RustLayout layout)
        {
            var asm = typeof(Program).Assembly;
            var destRoot = layout.WireCrateDir;
            Directory.CreateDirectory(Path.Combine(destRoot, "src"));

            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.Cargo.toml", Path.Combine(destRoot, "Cargo.toml"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.lib.rs", Path.Combine(destRoot, "src", "lib.rs"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.buffer.rs", Path.Combine(destRoot, "src", "buffer.rs"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.tls.rs", Path.Combine(destRoot, "src", "tls.rs"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.error.rs", Path.Combine(destRoot, "src", "error.rs"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.dispatch.rs", Path.Combine(destRoot, "src", "dispatch.rs"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.function.rs", Path.Combine(destRoot, "src", "function.rs"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.handle_buffer.rs", Path.Combine(destRoot, "src", "handle_buffer.rs"));
            ResourceWriter.WriteTextResource(asm, "extgen.Resources.Rust.gm_ext_wire.src.stream.rs", Path.Combine(destRoot, "src", "stream.rs"));
        }

        private static void EmitLibRs(RustLayout layout, RustEmitterContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ##### extgen :: Auto-generated file - do not edit (regenerated) #####");
            sb.AppendLine("// Extra `mod` lines: edit src/extra_mods.inc.rs (IfMissing).");
            sb.AppendLine("// User API: edit src/user/ (IfMissing).");
            sb.AppendLine();
            sb.AppendLine("#![allow(non_snake_case)]");
            sb.AppendLine();
            sb.AppendLine("pub mod generated;");
            sb.AppendLine("pub mod user;");
            sb.AppendLine();
            sb.AppendLine("include!(\"extra_mods.inc.rs\");");
            sb.AppendLine();
            sb.AppendLine("pub use generated::ffi;");
            sb.AppendLine("pub use generated::types;");
            sb.AppendLine("pub use generated::codecs;");
            sb.AppendLine();
            if (ctx.Settings.EmitAndroidJniModule)
            {
                sb.AppendLine("#[cfg(target_os = \"android\")]");
                sb.AppendLine("#[allow(non_snake_case)]");
                sb.AppendLine("#[path = \"generated/android_jni.rs\"]");
                sb.AppendLine("mod android_jni;");
                sb.AppendLine();
            }

            File.WriteAllText(Path.Combine(layout.SrcDir, "lib.rs"), sb.ToString(), new UTF8Encoding(false));

            var genMod = new StringBuilder();
            genMod.AppendLine("// ##### extgen :: Auto-generated #####");
            genMod.AppendLine("pub mod types;");
            genMod.AppendLine("pub mod codecs;");
            genMod.AppendLine("pub mod ffi;");
            File.WriteAllText(Path.Combine(layout.GeneratedDir, "mod.rs"), genMod.ToString(), new UTF8Encoding(false));

            ResourceWriter.WriteUtf8IfMissing(
                Path.Combine(layout.SrcDir, "extra_mods.inc.rs"),
                """
                // ##### extgen :: IfMissing - will not overwrite #####
                // Register additional crate modules here (same level as generated/ and user/):
                //
                //   mod my_helpers;
                //
                // Then add rust/src/my_helpers.rs. Regen keeps this file.

                """);
        }

        private static void EmitTypes(RustLayout layout, IrCompilation comp, RustTypeMap typeMap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ##### extgen :: Auto-generated file do not edit!! #####");
            sb.AppendLine();
            sb.AppendLine("#![allow(dead_code, non_camel_case_types, non_snake_case)]");
            sb.AppendLine();

            sb.AppendLine("pub mod constants {");
            foreach (var c in comp.Constants)
            {
                var name = RustCodeGen.SanitizeIdent(c.Name);
                var ty = typeMap.MapOwned(c.Type);
                sb.AppendLine($"    pub const {name}: {ty} = {c.Literal};");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("pub mod enums {");
            foreach (var e in comp.Enums)
            {
                var name = RustCodeGen.SanitizeIdent(e.Name);
                var underlying = MapEnumRepr(e.Underlying);
                sb.AppendLine($"    #[repr({underlying})]");
                sb.AppendLine("    #[derive(Clone, Copy, Debug, PartialEq, Eq)]");
                sb.AppendLine($"    pub enum {name} {{");
                foreach (var m in e.Members)
                {
                    var mName = RustCodeGen.SanitizeIdent(m.Name);
                    if (!string.IsNullOrWhiteSpace(m.DefaultLiteral))
                        sb.AppendLine($"        {mName} = {m.DefaultLiteral},");
                    else
                        sb.AppendLine($"        {mName},");
                }
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine($"    impl TryFrom<{underlying}> for {name} {{");
                sb.AppendLine("        type Error = ();");
                sb.AppendLine($"        fn try_from(v: {underlying}) -> Result<Self, ()> {{");
                sb.AppendLine("            match v {");
                foreach (var m in e.Members)
                {
                    var mName = RustCodeGen.SanitizeIdent(m.Name);
                    if (!string.IsNullOrWhiteSpace(m.DefaultLiteral))
                        sb.AppendLine($"                {m.DefaultLiteral} => Ok(Self::{mName}),");
                }
                sb.AppendLine("                _ => Err(()),");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine("pub mod structs {");
            var structsNeedEnums = comp.Structs.Any(s =>
                s.Fields.Any(f => MentionsEnum(f.Type)));
            if (structsNeedEnums)
                sb.AppendLine("    use super::enums;");
            sb.AppendLine();
            foreach (var s in comp.Structs)
            {
                var name = RustCodeGen.SanitizeIdent(s.Name);
                sb.AppendLine("    #[derive(Clone, Debug)]");
                sb.AppendLine($"    pub struct {name} {{");
                foreach (var f in s.Fields)
                {
                    var fName = RustCodeGen.SanitizeIdent(f.Name);
                    var fTy = RewriteFieldType(typeMap.MapOwned(f.Type));
                    sb.AppendLine($"        pub {fName}: {fTy},");
                }
                sb.AppendLine("    }");
                sb.AppendLine();
            }
            sb.AppendLine("}");

            File.WriteAllText(Path.Combine(layout.GeneratedDir, "types.rs"), sb.ToString(), new UTF8Encoding(false));
        }

        private static bool MentionsEnum(IrType t) =>
            t switch
            {
                IrType.Named { Kind: NamedKind.Enum } => true,
                IrType.Nullable n => MentionsEnum(n.Underlying),
                IrType.Array a => MentionsEnum(a.Element),
                _ => false
            };

        /// typeMap emits `structs::Y`; inside the structs module peer structs are bare names.
        private static string RewriteFieldType(string ty) =>
            ty.Replace("structs::", "", StringComparison.Ordinal);

        private static string MapEnumRepr(IrType underlying) =>
            underlying is IrType.Builtin b
                ? b.Kind switch
                {
                    BuiltinKind.Int8 => "i8",
                    BuiltinKind.UInt8 => "u8",
                    BuiltinKind.Int16 => "i16",
                    BuiltinKind.UInt16 => "u16",
                    BuiltinKind.Int32 => "i32",
                    BuiltinKind.UInt32 => "u32",
                    BuiltinKind.Int64 => "i64",
                    BuiltinKind.UInt64 => "u64",
                    _ => "i32"
                }
                : "i32";

        private static void EmitCodecs(
            RustLayout layout,
            IrCompilation comp,
            RustWireHelpers wire)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ##### extgen :: Auto-generated file do not edit!! #####");
            sb.AppendLine();
            sb.AppendLine("#![allow(dead_code, non_snake_case, unused_mut)]");
            sb.AppendLine();
            if (comp.Structs.Length > 0)
            {
                sb.AppendLine("use gm_ext_wire::{DataStream, GMBufferReader, GMValueOwned, GmStruct, GrowableWireWriter, WireByteWriter};");
                sb.AppendLine("use super::types::{enums, structs};");
                sb.AppendLine();
            }

            for (var i = 0; i < comp.Structs.Length; i++)
            {
                var s = comp.Structs[i];
                var id = RustCodeGen.SanitizeIdent(s.Name);
                sb.AppendLine($"pub const CODEC_ID_{id.ToUpperInvariant()}: u32 = {i};");
            }
            if (comp.Structs.Length > 0)
                sb.AppendLine();

            foreach (var s in comp.Structs)
            {
                var id = RustCodeGen.SanitizeIdent(s.Name);
                var fq = $"structs::{id}";

                sb.AppendLine($"pub fn codec_id_{id}() -> u32 {{ CODEC_ID_{id.ToUpperInvariant()} }}");
                sb.AppendLine();

                sb.AppendLine($"pub fn decode_{id}(mut r: &mut GMBufferReader<'_>) -> Option<{fq}> {{");
                foreach (var f in s.Fields)
                {
                    var fName = RustCodeGen.SanitizeIdent(f.Name);
                    sb.Append("    let ").Append(fName).Append(" = ").Append(wire.DecodeExpr(f.Type, "r")).Append(";\n");
                }
                sb.AppendLine($"    Some({fq} {{");
                foreach (var f in s.Fields)
                {
                    var fName = RustCodeGen.SanitizeIdent(f.Name);
                    sb.AppendLine($"        {fName},");
                }
                sb.AppendLine("    })");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine($"pub fn encode_{id}<W: WireByteWriter>(w: &mut W, obj: &{fq}) -> Option<()> {{");
                foreach (var f in s.Fields)
                {
                    var fName = RustCodeGen.SanitizeIdent(f.Name);
                    wire.EncodeStmt(sb, "    ", f.Type, $"obj.{fName}", "w");
                }
                sb.AppendLine("    Some(())");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine($"impl GmStruct for {fq} {{");
                sb.AppendLine($"    const CODEC_ID: u32 = CODEC_ID_{id.ToUpperInvariant()};");
                sb.AppendLine("    fn encode_fields<W: WireByteWriter>(&self, w: &mut W) -> Option<()> {");
                sb.AppendLine($"        encode_{id}(w, self)");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine($"pub fn write_typed_{id}<W: WireByteWriter>(w: &mut W, obj: &{fq}) -> Option<()> {{");
                sb.AppendLine($"    w.write_typed_struct_header(CODEC_ID_{id.ToUpperInvariant()})?;");
                sb.AppendLine($"    encode_{id}(w, obj)");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine($"pub fn write_typed_{id}_to_stream(ds: &mut DataStream, obj: &{fq}) -> Option<()> {{");
                sb.AppendLine("    ds.push_gm_struct(obj)");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            if (comp.Structs.Length > 0)
            {
                sb.AppendLine("pub fn typed_struct_to_owned(codec_id: u32, r: &mut GMBufferReader<'_>) -> Option<GMValueOwned> {");
                sb.AppendLine("    match codec_id {");
                foreach (var s in comp.Structs)
                {
                    var id = RustCodeGen.SanitizeIdent(s.Name);
                    sb.AppendLine($"        CODEC_ID_{id.ToUpperInvariant()} => {{");
                    sb.AppendLine($"            let obj = decode_{id}(r)?;");
                    sb.AppendLine("            let mut payload = Vec::new();");
                    sb.AppendLine("            {");
                    sb.AppendLine("                let mut w = GrowableWireWriter::new(&mut payload);");
                    sb.AppendLine($"                encode_{id}(&mut w, &obj)?;");
                    sb.AppendLine("            }");
                    sb.AppendLine("            Some(GMValueOwned::TypedStruct { codec_id, payload })");
                    sb.AppendLine("        }");
                }
                sb.AppendLine("        _ => None,");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            File.WriteAllText(Path.Combine(layout.GeneratedDir, "codecs.rs"), sb.ToString(), new UTF8Encoding(false));
        }

        private static void EmitFfi(
            RustLayout layout,
            RustEmitterContext ctx,
            IrCompilation comp,
            IReadOnlyList<IrFunction> functions,
            IReadOnlyList<NativeExportSpec> specs,
            RustWireHelpers wire)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ##### extgen :: Auto-generated file do not edit!! #####");
            sb.AppendLine();
            sb.AppendLine("#![allow(non_upper_case_globals)]");
            sb.AppendLine();
            var usesFunctions = comp.HasFunctionType();
            var usesBuffers = comp.HasBufferType();
            var rt = ctx.Runtime;

            sb.AppendLine("use std::ffi::c_char;");
            sb.AppendLine("use std::panic::catch_unwind;");
            sb.AppendLine("use gm_ext_wire::{clear_last_error, get_last_error_ptr, set_last_error};");
            if (specs.Any(s => s.ReturnType == ExportType.String))
                sb.AppendLine("use gm_ext_wire::store_tls_string;");
            var needsReader = functions.Any(IrAnalysis.NeedsArgsBuffer);
            var needsWriter = functions.Any(IrAnalysis.NeedsRetBuffer) || usesFunctions;
            if (needsReader || needsWriter || usesFunctions || usesBuffers)
            {
                var parts = new List<string>();
                if (needsReader) parts.Add("GMBufferReader");
                if (needsWriter) parts.Add("GMSliceWriter");
                if (usesFunctions) parts.Add("DispatchQueue");
                if (usesBuffers)
                {
                    parts.Add("BufferQueue");
                    parts.Add("GMBuffer");
                }
                sb.Append("use gm_ext_wire::{");
                sb.Append(string.Join(", ", parts));
                sb.AppendLine("};");
            }
            sb.AppendLine("use crate::user;");
            var needsCodecs = functions.Any(f => IrAnalysis.NeedsArgsBuffer(f) || IrAnalysis.NeedsRetBuffer(f));
            if (needsCodecs && comp.Structs.Length > 0)
                sb.AppendLine("use super::codecs;");
            else if (comp.Structs.Length > 0 && functions.Any(f => f.Parameters.Any(p =>
                         p.Type.ContainsBuiltin(BuiltinKind.Any)
                         || p.Type.ContainsBuiltin(BuiltinKind.AnyArray)
                         || p.Type.ContainsBuiltin(BuiltinKind.AnyMap))))
                sb.AppendLine("use super::codecs;");
            if (functions.Any(f =>
                    f.Parameters.Any(p => MentionsEnum(p.Type)) || MentionsEnum(f.ReturnType)))
                sb.AppendLine("use super::types::enums;");
            sb.AppendLine();

            if (usesFunctions)
            {
                sb.AppendLine($"static {rt.DispatchQueueField}: DispatchQueue = DispatchQueue::new();");
                sb.AppendLine();
                sb.AppendLine("#[no_mangle]");
                sb.AppendLine(
                    $"pub extern \"C\" fn {rt.NativePrefix}{comp.Name}_invocation_handler({rt.RetBufferParam}: *mut c_char, {rt.RetBufferLengthParam}: f64) -> f64 {{");
                sb.AppendLine("    match catch_unwind(|| {");
                sb.AppendLine("        clear_last_error();");
                sb.AppendLine(
                    $"        let mut __bw = unsafe {{ GMSliceWriter::from_raw_parts({rt.RetBufferParam} as *mut u8, {rt.RetBufferLengthParam} as usize) }};");
                sb.AppendLine($"        {rt.DispatchQueueField}.fetch(&mut __bw)");
                sb.AppendLine("    }) {");
                sb.AppendLine("        Ok(v) => v,");
                sb.AppendLine("        Err(_) => {");
                sb.AppendLine($"            set_last_error(\"panic in {rt.NativePrefix}{comp.Name}_invocation_handler\");");
                sb.AppendLine("            -1.0");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            if (usesBuffers)
            {
                sb.AppendLine($"static {rt.BufferQueueField}: BufferQueue = BufferQueue::new();");
                sb.AppendLine();
                sb.AppendLine("#[no_mangle]");
                sb.AppendLine(
                    $"pub extern \"C\" fn {rt.NativePrefix}{comp.Name}_queue_buffer({rt.ArgBufferParam}: *mut c_char, {rt.ArgBufferLengthParam}: f64) -> f64 {{");
                sb.AppendLine("    match catch_unwind(|| {");
                sb.AppendLine("        clear_last_error();");
                sb.AppendLine(
                    $"        let __buff = GMBuffer::new({rt.ArgBufferParam} as *mut u8, {rt.ArgBufferLengthParam} as u64);");
                sb.AppendLine($"        {rt.BufferQueueField}.push(__buff);");
                sb.AppendLine("        1.0");
                sb.AppendLine("    }) {");
                sb.AppendLine("        Ok(v) => v,");
                sb.AppendLine("        Err(_) => {");
                sb.AppendLine($"            set_last_error(\"panic in {rt.NativePrefix}{comp.Name}_queue_buffer\");");
                sb.AppendLine("            -1.0");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            sb.AppendLine("#[no_mangle]");
            sb.AppendLine($"pub extern \"C\" fn {rt.NativePrefix}{comp.Name}_get_last_error() -> *const c_char {{");
            sb.AppendLine("    get_last_error_ptr()");
            sb.AppendLine("}");
            sb.AppendLine();

            for (var i = 0; i < functions.Count; i++)
            {
                EmitOneFfi(sb, ctx, functions[i], specs[i], wire, attachTypedStructDecoder: comp.Structs.Length > 0);
            }

            File.WriteAllText(Path.Combine(layout.GeneratedDir, "ffi.rs"), sb.ToString(), new UTF8Encoding(false));
        }

        private static void EmitOneFfi(
            StringBuilder sb,
            RustEmitterContext ctx,
            IrFunction fn,
            NativeExportSpec spec,
            RustWireHelpers wire,
            bool attachTypedStructDecoder)
        {
            var ret = spec.ReturnType.AsRustType();
            var paramsList = RustCodeGen.RustParamList(spec.Params);
            var userName = RustCodeGen.UserFnName(spec.FunctionName);
            var rt = ctx.Runtime;

            sb.AppendLine("#[no_mangle]");
            sb.AppendLine($"pub extern \"C\" fn {spec.NativeSymbol}({paramsList}) -> {ret} {{");
            sb.AppendLine("    match catch_unwind(|| {");
            sb.AppendLine("        clear_last_error();");

            if (spec.NeedsArgsBuffer || spec.NeedsRetBuffer)
            {
                EmitBufferModeBody(sb, fn, spec, wire, userName, rt, attachTypedStructDecoder);
            }
            else
            {
                EmitDirectModeBody(sb, spec, userName);
            }

            sb.AppendLine("    }) {");
            sb.AppendLine("        Ok(v) => v,");
            sb.AppendLine("        Err(_) => {");
            sb.AppendLine($"            set_last_error(\"panic in {spec.NativeSymbol}\");");
            if (spec.ReturnType == ExportType.String)
                sb.AppendLine("            std::ptr::null()");
            else
                sb.AppendLine("            -1.0");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        private static void EmitBufferModeBody(
            StringBuilder sb,
            IrFunction fn,
            NativeExportSpec spec,
            RustWireHelpers wire,
            string userName,
            RuntimeNaming rt,
            bool attachTypedStructDecoder)
        {
            var stringRet = spec.ReturnType == ExportType.String && !spec.NeedsRetBuffer;
            var optionTy = stringRet ? "Option<String>" : "Option<f64>";
            sb.AppendLine($"        let __wire: {optionTy} = (|| {{");

            var callArgs = new List<string>();
            if (spec.NeedsArgsBuffer)
            {
                var needsAnyUnpack = fn.Parameters.Any(p =>
                    p.Type.ContainsBuiltin(BuiltinKind.Any)
                    || p.Type.ContainsBuiltin(BuiltinKind.AnyArray)
                    || p.Type.ContainsBuiltin(BuiltinKind.AnyMap));
                sb.AppendLine($"            let mut __br = unsafe {{ GMBufferReader::from_raw_parts({rt.ArgBufferParam} as *const u8, {rt.ArgBufferLengthParam} as usize) }};");
                if (needsAnyUnpack && attachTypedStructDecoder)
                {
                    sb.AppendLine("            let mut __br = __br.with_typed_struct_owned_decoder(codecs::typed_struct_to_owned);");
                }
                foreach (var p in fn.Parameters)
                {
                    var id = RustCodeGen.SanitizeIdent(p.Name);
                    sb.Append("            let ").Append(id).Append(" = ").Append(wire.DecodeExpr(p.Type, "__br")).Append(";\n");
                    callArgs.Add(id);
                }
            }
            else
            {
                foreach (var p in IrAnalysis.DirectArgs(fn))
                {
                    var id = RustCodeGen.SanitizeIdent(p.Name);
                    if (p.Type.IsStringScalar())
                    {
                        sb.AppendLine($"            let {id}_str = if {id}.is_null() {{ \"\" }} else {{ unsafe {{ std::ffi::CStr::from_ptr({id}) }}.to_str().unwrap_or(\"\") }};");
                        callArgs.Add($"{id}_str");
                    }
                    else
                    {
                        callArgs.Add(id);
                    }
                }
            }

            var isVoid = fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Void };
            var call = $"user::{userName}({string.Join(", ", callArgs)})";

            if (spec.NeedsRetBuffer)
            {
                if (isVoid)
                {
                    sb.AppendLine($"            {call};");
                    sb.AppendLine("            Some(0.0)");
                }
                else
                {
                    sb.AppendLine($"            let __result = {call};");
                    sb.AppendLine($"            let mut __bw = unsafe {{ GMSliceWriter::from_raw_parts({rt.RetBufferParam} as *mut u8, {rt.RetBufferLengthParam} as usize) }};");
                    wire.EncodeStmt(sb, "            ", fn.ReturnType, "__result", "__bw");
                    sb.AppendLine("            Some(0.0)");
                }
            }
            else if (stringRet)
            {
                sb.AppendLine($"            Some({call})");
            }
            else if (isVoid)
            {
                sb.AppendLine($"            {call};");
                sb.AppendLine("            Some(0.0)");
            }
            else if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Bool })
            {
                sb.AppendLine($"            Some(if {call} {{ 1.0 }} else {{ 0.0 }})");
            }
            else if (fn.ReturnType is IrType.Named { Kind: NamedKind.Enum })
            {
                sb.AppendLine($"            Some({call} as i32 as f64)");
            }
            else
            {
                sb.AppendLine($"            Some({call} as f64)");
            }

            sb.AppendLine("        })();");
            if (stringRet)
            {
                sb.AppendLine("        match __wire {");
                sb.AppendLine("            Some(s) => store_tls_string(s),");
                sb.AppendLine("            None => { set_last_error(\"wire decode/encode failed\"); std::ptr::null() }");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine("        match __wire {");
                sb.AppendLine("            Some(v) => v,");
                sb.AppendLine("            None => { set_last_error(\"wire decode/encode failed\"); -1.0 }");
                sb.AppendLine("        }");
            }
        }

        private static void EmitDirectModeBody(StringBuilder sb, NativeExportSpec spec, string userName)
        {
            var userArgs = new List<string>();
            foreach (var p in spec.Params)
            {
                var id = RustCodeGen.SanitizeIdent(p.Name);
                if (p.HostType == ExportType.String)
                {
                    sb.AppendLine($"        let {id}_str = if {id}.is_null() {{ \"\" }} else {{ unsafe {{ std::ffi::CStr::from_ptr({id}) }}.to_str().unwrap_or(\"\") }};");
                    userArgs.Add($"{id}_str");
                }
                else
                {
                    userArgs.Add(id);
                }
            }

            var call = $"user::{userName}({string.Join(", ", userArgs)})";
            if (spec.ReturnType == ExportType.String)
            {
                sb.AppendLine($"        let s = {call};");
                sb.AppendLine("        store_tls_string(s)");
            }
            else
            {
                sb.AppendLine($"        {call}");
            }
        }

        private static void EmitUserSurface(
            RustLayout layout,
            IReadOnlyList<IrFunction> functions,
            IReadOnlyList<NativeExportSpec> specs,
            RustTypeMap typeMap)
        {
            ResourceWriter.WriteUtf8IfMissing(
                Path.Combine(layout.UserDir, "mod.rs"),
                """
                // ##### extgen :: IfMissing - will not overwrite #####
                // Export surface called from generated/ffi.rs.
                // Optional: `mod helpers;` for files under this folder.

                mod impl_user;
                pub use impl_user::*;

                """);

            EmitUserImpl(layout, functions, specs, typeMap);
        }

        private static void EmitUserImpl(
            RustLayout layout,
            IReadOnlyList<IrFunction> functions,
            IReadOnlyList<NativeExportSpec> specs,
            RustTypeMap typeMap)
        {
            var path = Path.Combine(layout.UserDir, "impl_user.rs");
            if (File.Exists(path))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("// User implementations — edit this file (extgen will not overwrite).");
            sb.AppendLine("#![allow(unused_variables)]");
            sb.AppendLine();

            var needsTypes = functions.Any(f =>
                IrAnalysis.NeedsArgsBuffer(f) || IrAnalysis.NeedsRetBuffer(f) ||
                f.Parameters.Any(p => p.Type is IrType.Named) ||
                f.ReturnType is IrType.Named);
            var needsCChar = specs.Any(s =>
                (!s.NeedsArgsBuffer && !s.NeedsRetBuffer && s.Params.Any(p => p.HostType == ExportType.Pointer)));

            if (needsCChar)
                sb.AppendLine("use std::ffi::c_char;");
            if (needsTypes)
            {
                var needEnums = functions.Any(f =>
                    f.Parameters.Any(p => MentionsEnum(p.Type)) || MentionsEnum(f.ReturnType));
                var needStructs = functions.Any(f =>
                    f.Parameters.Any(p => MentionsStruct(p.Type)) || MentionsStruct(f.ReturnType));
                if (needEnums && needStructs)
                    sb.AppendLine("use crate::generated::types::{enums, structs};");
                else if (needEnums)
                    sb.AppendLine("use crate::generated::types::enums;");
                else if (needStructs)
                    sb.AppendLine("use crate::generated::types::structs;");
            }
            if (needsCChar || needsTypes)
                sb.AppendLine();

            for (var i = 0; i < functions.Count; i++)
            {
                var fn = functions[i];
                var spec = specs[i];
                var name = RustCodeGen.UserFnName(fn.Name);

                if (spec.NeedsArgsBuffer || spec.NeedsRetBuffer)
                {
                    var parts = new List<string>();
                    if (spec.NeedsArgsBuffer)
                    {
                        foreach (var p in fn.Parameters)
                        {
                            var id = RustCodeGen.SanitizeIdent(p.Name);
                            parts.Add($"{id}: {typeMap.MapParam(p.Type)}");
                        }
                    }
                    else
                    {
                        foreach (var p in IrAnalysis.DirectArgs(fn))
                        {
                            var id = RustCodeGen.SanitizeIdent(p.Name);
                            if (p.Type.IsStringScalar())
                                parts.Add($"{id}: &str");
                            else if (p.Type.IsPointerScalar())
                                parts.Add($"{id}: *mut c_char");
                            else
                                parts.Add($"{id}: f64");
                        }
                    }

                    var retTy = UserReturnType(fn, spec, typeMap);
                    sb.AppendLine($"pub fn {name}({string.Join(", ", parts)}) -> {retTy} {{");
                    sb.AppendLine(DefaultReturn(retTy));
                    sb.AppendLine("}");
                }
                else
                {
                    var parts = new List<string>();
                    foreach (var p in spec.Params)
                    {
                        var id = RustCodeGen.SanitizeIdent(p.Name);
                        parts.Add(p.HostType switch
                        {
                            ExportType.String => $"{id}: &str",
                            ExportType.Pointer => $"{id}: *mut c_char",
                            _ => $"{id}: f64"
                        });
                    }
                    var ret = spec.ReturnType == ExportType.String ? "String" : "f64";
                    sb.AppendLine($"pub fn {name}({string.Join(", ", parts)}) -> {ret} {{");
                    sb.AppendLine(DefaultReturn(ret));
                    sb.AppendLine("}");
                }
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static bool MentionsStruct(IrType t) =>
            t switch
            {
                IrType.Named { Kind: NamedKind.Struct } => true,
                IrType.Nullable n => MentionsStruct(n.Underlying),
                IrType.Array a => MentionsStruct(a.Element),
                _ => false
            };

        private static string UserReturnType(IrFunction fn, NativeExportSpec spec, RustTypeMap typeMap)
        {
            if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Void })
                return "()";
            if (spec.NeedsRetBuffer)
                return typeMap.MapOwned(fn.ReturnType);
            if (spec.ReturnType == ExportType.String)
                return "String";
            if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Bool })
                return "bool";
            if (fn.ReturnType is IrType.Named { Kind: NamedKind.Enum })
                return typeMap.MapOwned(fn.ReturnType);
            return "f64";
        }

        private static string DefaultReturn(string retTy) =>
            retTy switch
            {
                "()" => "    // TODO",
                "String" => "    String::new()",
                "bool" => "    false",
                "f64" => "    0.0",
                "gm_ext_wire::DataStream" => "    gm_ext_wire::DataStream::new()",
                "gm_ext_wire::ArrayStream" => "    gm_ext_wire::ArrayStream::new()",
                "gm_ext_wire::StructStream" => "    gm_ext_wire::StructStream::new()",
                _ when retTy.StartsWith("Option<", StringComparison.Ordinal) => "    None",
                _ when retTy.StartsWith("Vec<", StringComparison.Ordinal) => "    Vec::new()",
                _ when retTy.StartsWith("std::collections::HashMap<", StringComparison.Ordinal) =>
                    "    std::collections::HashMap::new()",
                _ when retTy.StartsWith("enums::", StringComparison.Ordinal) =>
                    "    // TODO: return an enum variant\n    unimplemented!()",
                _ when retTy.StartsWith("structs::", StringComparison.Ordinal) =>
                    "    // TODO: return a struct value\n    unimplemented!()",
                _ => "    unimplemented!()"
            };
    }
}
