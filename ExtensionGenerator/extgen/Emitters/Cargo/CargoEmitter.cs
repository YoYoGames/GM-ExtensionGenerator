using extgen.Models;
using extgen.Models.Config;
using extgen.Utils;
using extgen.Emitters.Rust;
using System.Text;

namespace extgen.Emitters.Cargo
{
    /// <summary>
    /// Emits Cargo.toml, .cargo/config.toml, and platform build/deploy scripts.
    /// </summary>
    internal sealed class CargoEmitter(CargoEmitterSettings settings, RuntimeNaming runtime) : global::extgen.Emitters.IBuildSystemEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            _ = runtime;
            var rustRoot = Path.GetFullPath(Path.Combine(outputDir, "rust"));
            Directory.CreateDirectory(rustRoot);
            Directory.CreateDirectory(Path.Combine(rustRoot, ".cargo"));
            Directory.CreateDirectory(Path.Combine(outputDir, "scripts"));

            EmitCargoToml(rustRoot, comp);
            EmitCargoConfig(rustRoot);
            EmitScripts(outputDir, rustRoot, comp);
        }

        private void EmitCargoToml(string rustRoot, IrCompilation comp)
        {
            // IfMissing: do not wipe hand-maintained deps (e.g. RustySDF: rustybuzz, fdsm, …).
            var path = Path.Combine(rustRoot, "Cargo.toml");
            if (File.Exists(path))
                return;

            var crate = RustEmitterSettings.SanitizeCrateName(comp.Name);

            var sb = new StringBuilder();
            sb.AppendLine("[package]");
            sb.AppendLine($"name = \"{crate}\"");
            sb.AppendLine("version = \"0.1.0\"");
            sb.AppendLine("edition = \"2021\"");
            sb.AppendLine();
            sb.AppendLine("[lib]");
            sb.AppendLine("crate-type = [\"cdylib\", \"staticlib\", \"rlib\"]");
            sb.AppendLine("path = \"src/lib.rs\"");
            sb.AppendLine();
            sb.AppendLine("[dependencies]");
            sb.AppendLine("gm_ext_wire = { path = \"crates/gm_ext_wire\" }");
            sb.AppendLine();
            if (settings.HasAndroid)
            {
                sb.AppendLine("[target.'cfg(target_os = \"android\")'.dependencies]");
                sb.AppendLine("jni = \"0.21\"");
                sb.AppendLine();
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private void EmitCargoConfig(string rustRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ##### extgen :: generated #####");
            sb.AppendLine("[target.x86_64-pc-windows-msvc]");
            sb.AppendLine("rustflags = [\"-C\", \"target-feature=+crt-static\"]");
            sb.AppendLine();
            sb.AppendLine("[target.aarch64-linux-android]");
            sb.AppendLine("rustflags = [\"-C\", \"link-arg=-Wl,-z,max-page-size=16384\"]");
            sb.AppendLine();
            sb.AppendLine("[target.armv7-linux-androideabi]");
            sb.AppendLine("rustflags = [\"-C\", \"link-arg=-Wl,-z,max-page-size=16384\"]");
            sb.AppendLine();
            sb.AppendLine("[target.x86_64-linux-android]");
            sb.AppendLine("rustflags = [\"-C\", \"link-arg=-Wl,-z,max-page-size=16384\"]");
            sb.AppendLine();
            File.WriteAllText(Path.Combine(rustRoot, ".cargo", "config.toml"), sb.ToString(), new UTF8Encoding(false));
        }

        private void EmitScripts(string outputDir, string rustRoot, IrCompilation comp)
        {
            var asm = typeof(Program).Assembly;
            var scripts = Path.Combine(outputDir, "scripts");
            var extgenScripts = Path.Combine(scripts, "extgen");
            Directory.CreateDirectory(extgenScripts);

            // Prefer hand-maintained [package].name when Cargo.toml already exists (IfMissing),
            // but always sanitize so hyphens become underscores (matches cargo's DLL/SO stem).
            var crate = RustEmitterSettings.SanitizeCrateName(
                TryReadCargoPackageName(Path.Combine(rustRoot, "Cargo.toml")) ?? comp.Name);
            var extName = settings.ExtensionName;
            if (string.IsNullOrWhiteSpace(extName) || extName == "MyExtension")
                extName = comp.Name;

            var tokens = new Dictionary<string, string>
            {
                ["EXTGEN_CRATE_NAME"] = crate,
                ["EXTGEN_EXTENSION_NAME"] = extName,
                ["EXTGEN_COMP_NAME"] = comp.Name
            };

            // Always refresh generated cores under scripts/extgen/
            ResourceWriter.WriteTemplatedTextResource(
                asm,
                "extgen.Resources.Rust.scripts.build_windows.bat",
                Path.Combine(extgenScripts, "build_windows.bat"),
                tokens);

            ResourceWriter.WriteTemplatedTextResource(
                asm,
                "extgen.Resources.Rust.scripts.build_android.sh",
                Path.Combine(extgenScripts, "build_android.sh"),
                tokens);

            ResourceWriter.WriteTemplatedTextResource(
                asm,
                "extgen.Resources.Rust.scripts.build_ios.sh",
                Path.Combine(extgenScripts, "build_ios.sh"),
                tokens);

            // User entrypoints: IfMissing (same idea as src/CMakeLists.txt / Counter_native.cpp)
            ResourceWriter.WriteTemplatedTextResource(
                asm,
                "extgen.Resources.Rust.scripts.build_windows_wrapper.bat",
                Path.Combine(scripts, "build_windows.bat"),
                tokens,
                replace: false);

            ResourceWriter.WriteTemplatedTextResource(
                asm,
                "extgen.Resources.Rust.scripts.build_android_wrapper.sh",
                Path.Combine(scripts, "build_android.sh"),
                tokens,
                replace: false);

            ResourceWriter.WriteTemplatedTextResource(
                asm,
                "extgen.Resources.Rust.scripts.build_ios_wrapper.sh",
                Path.Combine(scripts, "build_ios.sh"),
                tokens,
                replace: false);
        }

        private static string? TryReadCargoPackageName(string cargoTomlPath)
        {
            if (!File.Exists(cargoTomlPath))
                return null;

            try
            {
                var inPackage = false;
                foreach (var raw in File.ReadLines(cargoTomlPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                        continue;
                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        inPackage = string.Equals(line, "[package]", StringComparison.Ordinal);
                        continue;
                    }
                    if (!inPackage)
                        continue;
                    if (!line.StartsWith("name", StringComparison.Ordinal))
                        continue;
                    var eq = line.IndexOf('=');
                    if (eq < 0)
                        continue;
                    var value = line[(eq + 1)..].Trim().Trim('"').Trim('\'');
                    return string.IsNullOrWhiteSpace(value) ? null : value;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
