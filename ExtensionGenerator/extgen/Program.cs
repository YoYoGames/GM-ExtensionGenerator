using extgen.Emitters;
using extgen.Emitters.Cmake;
using extgen.Emitters.Cpp;
using extgen.Emitters.Doc;
using extgen.Emitters.Gml;
using extgen.Emitters.GmlRuntime;
using extgen.Emitters.Java;
using extgen.Emitters.Jni;
using extgen.Emitters.Kotlin;
using extgen.Emitters.Objc;
using extgen.Emitters.ObjcNative;
using extgen.Emitters.Swift;
using extgen.Emitters.Yy;
using extgen.Model;
using extgen.Options;
using extgen.Parsing.Gmidl;
using NDesk.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace extgen
{
    public static class Program
    {
        private static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static int Main(string[] args)
        {
            string? configPath = null;
            bool showHelp = false;

            var options = new OptionSet {
                { "c|config=", "Path to JSON config file.", v => configPath = v },
                { "h|help",    "Show help.", v => showHelp = v != null }
            };

            try
            {
                var extras = options.Parse(args);

                if (showHelp || string.IsNullOrWhiteSpace(configPath) || extras.Count > 0)
                {
                    ShowUsage(options);
                    return showHelp ? 0 : 1;
                }

                return RunFromJsonConfig(configPath!);
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine(e.Message);
                ShowUsage(options);
                return 2;
            }
            catch (JsonException je)
            {
                Console.Error.WriteLine($"Config JSON error: {je.Message}");
                return 11;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 99;
            }
        }

        private static void ShowUsage(OptionSet options)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  codegen --config <path/to/config.json>");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  # macOS/Linux (bash/zsh)");
            Console.WriteLine("  codegen --config './configs/game config.json'");
            Console.WriteLine("  # Windows PowerShell");
            Console.WriteLine(@"  codegen --config '.\configs\game config.json'");
            Console.WriteLine("  # Windows CMD.exe (use double quotes!)");
            Console.WriteLine(@"  codegen --config "".\configs\game config.json""");
            Console.WriteLine();
            options.WriteOptionDescriptions(Console.Out);
        }

        // ---------- JSON flow ----------
        private static int RunFromJsonConfig(string configPath)
        {
            var fullConfigPath = Path.GetFullPath(configPath);
            if (!File.Exists(fullConfigPath))
            {
                Console.Error.WriteLine($"Config file not found: {fullConfigPath}");
                return 3;
            }

            var json = File.ReadAllText(fullConfigPath, Encoding.UTF8);
            var cfg = JsonSerializer.Deserialize<CodegenConfig>(json, jsonSerializerOptions)
                      ?? throw new JsonException("Empty or invalid configuration.");

            var baseDir = Path.GetDirectoryName(fullConfigPath)!;
            var inputPath = ResolvePath(cfg.Input, baseDir);
            var outputDir = ResolvePath(cfg.OutputDir, baseDir);

            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Input file not found: {inputPath ?? "(null)"}");
                return 4;
            }
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Console.Error.WriteLine("Missing 'outputDir' in config.");
                return 5;
            }

            Directory.CreateDirectory(outputDir);

            IrCompilation? compilation;
            try
            {
                compilation = GmidlSchemaLoader.LoadFromFile(inputPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 6;
            }

            var emitters = BuildEmittersFromConfig(cfg);

            var cmakeEmitter = new CmakeEmitter(cfg, emitters);
            cmakeEmitter.Emit(compilation, outputDir);

            if (emitters.Count == 0)
            {
                Console.WriteLine("[CodeGen] No targets enabled in config. Nothing to generate.");
                return 0;
            }
            else
            {
                // Run Emitters
                foreach (var (lang, em) in emitters)
                {
                    Console.WriteLine($"[CodeGen] {lang.ToUpperInvariant()} -> {outputDir}");
                    em.Emit(compilation, outputDir);
                }
            }

            Console.WriteLine("[CodeGen] Success [x]");
            return 0;
        }

        private static string ResolvePath(string? path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            // Support ~ and %ENV% / $ENV_VAR
            var expanded = Environment.ExpandEnvironmentVariables(
                path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(baseDir, expanded));
        }

        private static Dictionary<string, IIrEmitter> BuildEmittersFromConfig(CodegenConfig options)
        {
            var emitters = new Dictionary<string, IIrEmitter>(StringComparer.OrdinalIgnoreCase);

            if (options.Cpp is { Enabled: true }) emitters["cpp"] = new CppEmitter(options.Cpp, options.Runtime);
            if (options.Gml is { Enabled: true }) emitters["gml"] = new GmlEmitter(options.Gml);
            if (options.Yy is { Enabled: true }) emitters["yy"] = new YyEmitter(options.Yy, options.Runtime);

            // Only do one or the other (Java / JNI)
            if (options.Kotlin is { Enabled: true }) emitters["android"] = new KotlinEmitter(options.Kotlin, options.Runtime);
            if (options.Java is { Enabled: true }) emitters["android"] = new JavaEmitter(options.Java, options.Runtime);
            if (options.Jni is { Enabled: true }) emitters["android"] = new JniEmitter(options.Jni, options.Runtime);

            // Only do one or the other (Objc or Swift or Native)
            if (options.Ios is { Enabled: true }) emitters["ios"] = new ObjcEmitter(options.Ios, options.Runtime);
            if (options.IosSwift is { Enabled: true }) emitters["ios"] = new SwiftEmitter(options.IosSwift, options.Runtime);
            if (options.IosNative is { Enabled: true }) emitters["ios"] = new ObjcNativeEmitter(options.IosNative, options.Runtime);

            // Only do one or the other (Objc or Swift or Native)
            if (options.Tvos is { Enabled: true }) emitters["tvos"] = new ObjcEmitter(options.Tvos, options.Runtime);
            if (options.TvosSwift is { Enabled: true }) emitters["tvos"] = new SwiftEmitter(options.TvosSwift, options.Runtime);
            if (options.TvosNative is { Enabled: true }) emitters["tvos"] = new ObjcNativeEmitter(options.TvosNative, options.Runtime);

            // Generate documentation
            if (options.Docs is { Enabled: true }) emitters["docs"] = new DocEmitter(options.Docs, options.Runtime);

            // Emit the ExtensionCore runtime
            if (options.GmlRuntime is { Enabled: true }) emitters["gml_runtime"] = new GmlRuntimeEmitter(options.GmlRuntime);

            return emitters;
        }
    }
}