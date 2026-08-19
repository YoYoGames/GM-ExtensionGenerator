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
            // IfMissing: do not wipe hand-maintained user deps in an existing Cargo.toml.
            var path = Path.Combine(rustRoot, "Cargo.toml");
            if (File.Exists(path))
                return;

            var crate = settings.CrateName;
            if (string.IsNullOrWhiteSpace(crate))
                crate = RustEmitterSettings.SanitizeCrateName(comp.Name);

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

            sb.AppendLine("[profile.release]");
            sb.AppendLine("lto = \"thin\"");
            sb.AppendLine("strip = true");
            sb.AppendLine("panic = \"unwind\"");
            sb.AppendLine();

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private void EmitCargoConfig(string rustRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ##### extgen :: generated #####");

            var needsAppleEnv = settings.HasMac || settings.HasIos || settings.HasTvos;
            if (needsAppleEnv)
            {
                sb.AppendLine("[env]");
                if (settings.HasMac)
                    sb.AppendLine($"MACOSX_DEPLOYMENT_TARGET = \"{settings.MacosMinVersion}\"");
                if (settings.HasIos)
                    sb.AppendLine($"IPHONEOS_DEPLOYMENT_TARGET = \"{settings.IosMinVersion}\"");
                if (settings.HasTvos)
                    sb.AppendLine($"TVOS_DEPLOYMENT_TARGET = \"{settings.TvosMinVersion}\"");
                sb.AppendLine();
            }

            if (settings.HasWindows)
            {
                sb.AppendLine("[target.x86_64-pc-windows-msvc]");
                sb.AppendLine("rustflags = [\"-C\", \"target-feature=+crt-static\"]");
                sb.AppendLine();
            }

            if (settings.HasAndroid)
            {
                const string androidRustflags =
                    "rustflags = [" +
                    "\"-C\", \"link-arg=-Wl,-z,max-page-size=16384\", " +
                    "\"-C\", \"link-arg=-llog\", " +
                    "\"-C\", \"link-arg=-landroid\"" +
                    "]";
                sb.AppendLine("[target.aarch64-linux-android]");
                sb.AppendLine(androidRustflags);
                sb.AppendLine();
                sb.AppendLine("[target.armv7-linux-androideabi]");
                sb.AppendLine(androidRustflags);
                sb.AppendLine();
                sb.AppendLine("[target.x86_64-linux-android]");
                sb.AppendLine(androidRustflags);
                sb.AppendLine();
            }

            // Dynamic Apple frameworks: @executable_path (GameMaker runners often lack LC_RPATH).
            var fw = settings.IosFrameworkName;
            var iosMin = settings.IosMinVersion;
            var tvosMin = settings.TvosMinVersion;
            var installName = $"@executable_path/Frameworks/{fw}.framework/{fw}";
            string AppleMobileFlags(string versionMinFlag, string minVer) =>
                "rustflags = [" +
                $"\"-C\", \"link-arg={versionMinFlag}{minVer}\", " +
                $"\"-C\", \"link-arg=-Wl,-install_name,{installName}\"" +
                "]";

            if (settings.HasIos)
            {
                sb.AppendLine("[target.aarch64-apple-ios]");
                sb.AppendLine(AppleMobileFlags("-miphoneos-version-min=", iosMin));
                sb.AppendLine();
                sb.AppendLine("[target.aarch64-apple-ios-sim]");
                sb.AppendLine(AppleMobileFlags("-miphonesimulator-version-min=", iosMin));
                sb.AppendLine();
                sb.AppendLine("[target.x86_64-apple-ios]");
                sb.AppendLine(AppleMobileFlags("-miphonesimulator-version-min=", iosMin));
                sb.AppendLine();
            }

            if (settings.HasTvos)
            {
                sb.AppendLine("[target.aarch64-apple-tvos]");
                sb.AppendLine(AppleMobileFlags("-mappletvos-version-min=", tvosMin));
                sb.AppendLine();
                sb.AppendLine("[target.aarch64-apple-tvos-sim]");
                sb.AppendLine(AppleMobileFlags("-mappletvsimulator-version-min=", tvosMin));
                sb.AppendLine();
                sb.AppendLine("[target.x86_64-apple-tvos]");
                sb.AppendLine(AppleMobileFlags("-mappletvsimulator-version-min=", tvosMin));
                sb.AppendLine();
            }

            File.WriteAllText(Path.Combine(rustRoot, ".cargo", "config.toml"), sb.ToString(), new UTF8Encoding(false));
        }

        private void EmitScripts(string outputDir, string rustRoot, IrCompilation comp)
        {
            var asm = typeof(Program).Assembly;
            var scripts = Path.Combine(outputDir, "scripts");
            var extgenScripts = Path.Combine(scripts, "extgen");
            Directory.CreateDirectory(extgenScripts);

            // Prefer existing Cargo.toml [package].name; else config build.rust.crateName / extension name.
            var crate = RustEmitterSettings.SanitizeCrateName(
                TryReadCargoPackageName(Path.Combine(rustRoot, "Cargo.toml"))
                ?? settings.CrateName
                ?? comp.Name);
            var extName = settings.ExtensionName;
            if (string.IsNullOrWhiteSpace(extName) || extName == "MyExtension")
                extName = comp.Name;

            var fwName = string.IsNullOrWhiteSpace(settings.IosFrameworkName)
                ? $"{extName}_Rust"
                : settings.IosFrameworkName;

            var tokens = new Dictionary<string, string>
            {
                ["EXTGEN_CRATE_NAME"] = crate,
                ["EXTGEN_EXTENSION_NAME"] = extName,
                ["EXTGEN_COMP_NAME"] = comp.Name,
                ["EXTGEN_WINDOWS_OUTPUT_FOLDER"] = settings.WindowsOutput.Replace('\\', '/'),
                ["EXTGEN_MACOS_OUTPUT_FOLDER"] = settings.MacosOutput.Replace('\\', '/'),
                ["EXTGEN_LINUX_OUTPUT_FOLDER"] = settings.LinuxOutput.Replace('\\', '/'),
                ["EXTGEN_ANDROID_OUTPUT_FOLDER"] = settings.AndroidOutput.Replace('\\', '/'),
                ["EXTGEN_IOS_OUTPUT_FOLDER"] = settings.IosOutput.Replace('\\', '/'),
                ["EXTGEN_TVOS_OUTPUT_FOLDER"] = settings.TvosOutput.Replace('\\', '/'),
                ["EXTGEN_IOS_FRAMEWORK_NAME"] = fwName,
                ["EXTGEN_IOS_BUNDLE_ID"] = settings.IosBundleId,
                ["EXTGEN_IOS_MIN_VERSION"] = settings.IosMinVersion,
                ["EXTGEN_TVOS_MIN_VERSION"] = settings.TvosMinVersion,
            };

            void EmitCore(string resourceFile, string destName) =>
                ResourceWriter.WriteTemplatedTextResource(
                    asm,
                    $"extgen.Resources.Rust.scripts.{resourceFile}",
                    Path.Combine(extgenScripts, destName),
                    tokens);

            void EmitWrapper(string resourceFile, string destName) =>
                ResourceWriter.WriteTemplatedTextResource(
                    asm,
                    $"extgen.Resources.Rust.scripts.{resourceFile}",
                    Path.Combine(scripts, destName),
                    tokens,
                    replace: false);

            if (settings.HasWindows)
            {
                EmitCore("build_windows.bat", "build_windows.bat");
                EmitWrapper("build_windows_wrapper.bat", "build_windows.bat");
            }
            if (settings.HasAndroid)
            {
                EmitCore("build_android.sh", "build_android.sh");
                EmitWrapper("build_android_wrapper.sh", "build_android.sh");
            }
            if (settings.HasMac)
            {
                EmitCore("build_macos.sh", "build_macos.sh");
                EmitWrapper("build_macos_wrapper.sh", "build_macos.sh");
            }
            if (settings.HasLinux)
            {
                EmitCore("build_linux.sh", "build_linux.sh");
                EmitWrapper("build_linux_wrapper.sh", "build_linux.sh");
            }
            if (settings.HasIos)
            {
                EmitCore("build_ios.sh", "build_ios.sh");
                EmitWrapper("build_ios_wrapper.sh", "build_ios.sh");
            }
            if (settings.HasTvos)
            {
                EmitCore("build_tvos.sh", "build_tvos.sh");
                EmitWrapper("build_tvos_wrapper.sh", "build_tvos.sh");
            }
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
