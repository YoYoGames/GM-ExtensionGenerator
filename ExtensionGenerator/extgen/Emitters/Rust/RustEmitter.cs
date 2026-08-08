using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Utils;
using System.Text;

namespace extgen.Emitters.Rust
{
    /// <summary>
    /// Emits portable Rust FFI (`__EXT_NATIVE__*`) and user stubs.
    /// Mirrors C++ policy: generated/ always overwrite; user surface IfMissing.
    /// </summary>
    internal sealed class RustEmitter(RustEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            var ctx = new RustEmitterContext(comp.Name, settings, runtime);
            var layout = new RustLayout(outputDir);
            var specs = NativeExportSpec.FromCompilation(comp, runtime);

            EmitWireCrate(layout);
            EmitFfi(layout, ctx, comp, specs);
            EmitLibRs(layout, ctx);
            EmitUserSurface(layout, ctx, specs);
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
        }

        private static void EmitLibRs(RustLayout layout, RustEmitterContext ctx)
        {
            // Always overwrite — safe: no hand-edited mods belong here.
            // Extra crate modules go in extra_mods.inc.rs (IfMissing).
            var sb = new StringBuilder();
            sb.AppendLine("// ##### extgen :: Auto-generated file - do not edit (regenerated) #####");
            sb.AppendLine("// Extra `mod` lines: edit src/extra_mods.inc.rs (IfMissing).");
            sb.AppendLine("// User API: edit src/user/ (IfMissing).");
            sb.AppendLine();
            sb.AppendLine("#![allow(non_snake_case)]");
            sb.AppendLine();
            sb.AppendLine("mod generated;");
            sb.AppendLine("pub mod user;");
            sb.AppendLine();
            sb.AppendLine("include!(\"extra_mods.inc.rs\");");
            sb.AppendLine();
            sb.AppendLine("pub use generated::ffi;");
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
            genMod.AppendLine("pub mod ffi;");
            File.WriteAllText(Path.Combine(layout.GeneratedDir, "mod.rs"), genMod.ToString(), new UTF8Encoding(false));

            // C++ analogue of dropping new .cpp under src/native/: register mods here once.
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

        private static void EmitUserSurface(
            RustLayout layout,
            RustEmitterContext ctx,
            IReadOnlyList<NativeExportSpec> specs)
        {
            _ = ctx;
            ResourceWriter.WriteUtf8IfMissing(
                Path.Combine(layout.UserDir, "mod.rs"),
                """
                // ##### extgen :: IfMissing - will not overwrite #####
                // Export surface called from generated/ffi.rs.
                // Optional: `mod helpers;` for files under this folder.

                mod impl_user;
                pub use impl_user::*;

                """);

            EmitUserImpl(layout, specs);
        }

        private static void EmitFfi(
            RustLayout layout,
            RustEmitterContext ctx,
            IrCompilation comp,
            IReadOnlyList<NativeExportSpec> specs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// ##### extgen :: Auto-generated file do not edit!! #####");
            sb.AppendLine();
            sb.AppendLine("use std::ffi::c_char;");
            sb.AppendLine("use std::panic::catch_unwind;");
            sb.AppendLine("use gm_ext_wire::{clear_last_error, get_last_error_ptr, set_last_error, store_tls_string};");
            sb.AppendLine("use crate::user;");
            sb.AppendLine();

            // last_error export
            sb.AppendLine("#[no_mangle]");
            sb.AppendLine($"pub extern \"C\" fn {ctx.Runtime.NativePrefix}{comp.Name}_get_last_error() -> *const c_char {{");
            sb.AppendLine("    get_last_error_ptr()");
            sb.AppendLine("}");
            sb.AppendLine();

            foreach (var spec in specs)
            {
                EmitOneFfi(sb, ctx, spec);
            }

            File.WriteAllText(Path.Combine(layout.GeneratedDir, "ffi.rs"), sb.ToString(), new UTF8Encoding(false));
        }

        private static void EmitOneFfi(StringBuilder sb, RustEmitterContext ctx, NativeExportSpec spec)
        {
            var ret = spec.ReturnType.AsRustType();
            var paramsList = RustCodeGen.RustParamList(spec.Params);
            var userName = RustCodeGen.UserFnName(spec.FunctionName);

            sb.AppendLine("#[no_mangle]");
            sb.AppendLine($"pub extern \"C\" fn {spec.NativeSymbol}({paramsList}) -> {ret} {{");
            sb.AppendLine("    match catch_unwind(|| {");
            sb.AppendLine("        clear_last_error();");

            if (spec.NeedsArgsBuffer || spec.NeedsRetBuffer)
            {
                // Buffer-mode: forward raw pointers to user; user / generated helpers unpack via gm_ext_wire.
                var callArgs = string.Join(", ", spec.Params.Select(p => RustCodeGen.SanitizeIdent(p.Name)));
                if (spec.ReturnType == ExportType.String)
                {
                    sb.AppendLine($"        let s = user::{userName}({callArgs});");
                    sb.AppendLine("        store_tls_string(s)");
                }
                else if (spec.ReturnType == ExportType.Double)
                {
                    sb.AppendLine($"        user::{userName}({callArgs})");
                }
                else
                {
                    sb.AppendLine($"        user::{userName}({callArgs});");
                    sb.AppendLine("        std::ptr::null()");
                }
            }
            else
            {
                // Direct mode: convert C strings to &str; keep pointers as raw *mut c_char
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
                        // Double and Pointer forward unchanged (pointer is ABI-compatible with *mut u8)
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

        private static void EmitUserImpl(RustLayout layout, IReadOnlyList<NativeExportSpec> specs)
        {
            var path = Path.Combine(layout.UserDir, "impl_user.rs");
            if (File.Exists(path))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("// User implementations — edit this file (extgen will not overwrite).");
            sb.AppendLine("#![allow(unused_variables)]");
            sb.AppendLine();

            var needsCChar = specs.Any(s =>
                s.NeedsArgsBuffer || s.NeedsRetBuffer ||
                s.Params.Any(p => p.HostType == ExportType.Pointer));
            if (needsCChar)
            {
                sb.AppendLine("use std::ffi::c_char;");
                sb.AppendLine();
            }

            foreach (var spec in specs)
            {
                var name = RustCodeGen.UserFnName(spec.FunctionName);
                if (spec.NeedsArgsBuffer || spec.NeedsRetBuffer)
                {
                    var plist = RustCodeGen.RustParamList(spec.Params);
                    var ret = spec.ReturnType switch
                    {
                        ExportType.String => "String",
                        _ => "f64"
                    };
                    sb.AppendLine($"pub fn {name}({plist}) -> {ret} {{");
                    sb.AppendLine("    // Buffer-protocol export: unpack with gm_ext_wire::GMBufferReader");
                    if (spec.ReturnType == ExportType.String)
                        sb.AppendLine("    String::new()");
                    else
                        sb.AppendLine("    0.0");
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
                    if (spec.ReturnType == ExportType.String)
                        sb.AppendLine("    String::new()");
                    else
                        sb.AppendLine("    0.0");
                    sb.AppendLine("}");
                }
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
