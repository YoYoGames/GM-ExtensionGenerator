using extgen.Config;
using extgen.Config.Targets.Mobile;
using extgen.Emitters;
using extgen.Emitters.Android.Java;
using extgen.Emitters.Android.Jni;
using extgen.Emitters.Android.Kotlin;
using extgen.Emitters.AppleMobile;
using extgen.Emitters.AppleMobile.Objc;
using extgen.Emitters.AppleMobile.ObjcNative;
using extgen.Emitters.AppleMobile.Swift;
using extgen.Emitters.Cmake;
using extgen.Emitters.Cpp;
using extgen.Emitters.Doc;
using extgen.Emitters.Gml;
using extgen.Emitters.Yy;
using extgen.Model;
using extgen.Options.Android;
using extgen.Parsing.Gmidl;
using NDesk.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        public static int Main(string[] args)
        {
            string? configPath = null;
            string? initDir = null;
            bool showHelp = false;

            var options = new OptionSet {
                { "c|config=", "Path to JSON config file.", v => configPath = v },
                { "i|init=", "Initialize a new config + schema in the given folder.", v => initDir = v },
                { "h|help",    "Show help.", v => showHelp = v != null }
            };

            try
            {
                var extras = options.Parse(args);

                if (!string.IsNullOrWhiteSpace(initDir))
                {
                    return InitProject(initDir);
                }

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

            try
            {
                if (!EnsureSchemaBesideConfigAndPatchConfigJson(fullConfigPath)) 
                {
                    Console.WriteLine($"Config JSON updated with new patching schema.");
                    return 0;
                }
            }
            catch (JsonException je)
            {
                Console.Error.WriteLine($"Config JSON error while patching schema: {je.Message}");
                return 11;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to write schema/patch config: {ex}");
                return 12;
            }

            var json = File.ReadAllText(fullConfigPath, Encoding.UTF8);
            var cfg = JsonSerializer.Deserialize<ExtGenConfig>(json, jsonSerializerOptions)
                      ?? throw new JsonException("Empty or invalid configuration.");

            var baseDir = Path.GetDirectoryName(fullConfigPath)!;
            var inputPath = ResolvePath(cfg.Input, baseDir);
            var outputDir = ResolvePath(cfg.Root, baseDir);

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

            var cmakeEmitter = new CmakeEmitter(cfg);
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

        private static Dictionary<string, IIrEmitter> BuildEmittersFromConfig(ExtGenConfig cfg)
        {
            var plan = new EmitterPlan { Cfg = cfg };
            plan.Validate();

            var emitters = new Dictionary<string, IIrEmitter>(StringComparer.OrdinalIgnoreCase);

            AddCoreEmitters(emitters, plan);
            AddBindingEmitters(emitters, plan);

            AddAndroidEmitter(emitters, plan);
            AddIosEmitter(emitters, plan);
            AddTvosEmitter(emitters, plan);

            AddDocsEmitter(emitters, plan);

            return emitters;
        }

        private static void AddCoreEmitters(Dictionary<string, IIrEmitter> emitters, EmitterPlan plan)
        {
            if (!plan.NeedsCpp) return;

            // Build cpp options from target config (as you already do)
            var cppEmitterOptions = new CppEmitterOptions
            {
                SourceFilename = plan.Cfg.Targets.SourceFilename,
                SourceFolder = plan.Cfg.Targets.SourceFolder
            };

            emitters["cpp"] = new CppEmitter(cppEmitterOptions, plan.Cfg.Runtime);
        }

        private static void AddBindingEmitters(Dictionary<string, IIrEmitter> emitters, EmitterPlan plan)
        {
            if (!plan.AllowBindings) return;

            if (plan.Cfg.Gml is { Enabled: true } g)
            {
                var gmlOpts = GmlEmitterOptions.FromConfig(g);
                emitters["gml"] = new GmlEmitter(gmlOpts);

                // If YY generation is conceptually “part of bindings”, keep it here
                var yyOpts = YyEmitterOptions.FromConfig(g);
                emitters["yy"] = new YyEmitter(yyOpts, plan.Cfg.Runtime);
            }
        }

        private static void AddAndroidEmitter(Dictionary<string, IIrEmitter> emitters, EmitterPlan plan)
        {
            if (plan.Cfg.Targets.Android is not AndroidTargetConfig { Enabled: true } androidCfg)
                return;

            var androidOpts = AndroidEmitterOptions.FromConfig(androidCfg);

            emitters["android"] = plan.AndroidMode switch
            {
                AndroidMode.Kotlin => new KotlinEmitter(androidOpts, plan.Cfg.Runtime),
                AndroidMode.Java => new JavaEmitter(androidOpts, plan.Cfg.Runtime),
                AndroidMode.Jni => new JniEmitter(androidOpts, plan.Cfg.Runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(plan.AndroidMode), plan.AndroidMode, "Unknown AndroidMode")
            };
        }

        private static void AddIosEmitter(Dictionary<string, IIrEmitter> emitters, EmitterPlan plan)
        {
            if (plan.Cfg.Targets.Ios is not IosTargetConfig { Enabled: true } iosCfg)
                return;

            var iosOpts = IosEmitterOptions.FromConfig(iosCfg);

            emitters["ios"] = plan.IosMode switch
            {
                AppleMobileMode.Objc => new ObjcEmitter(iosOpts, plan.Cfg.Runtime),
                AppleMobileMode.Swift => new SwiftEmitter(iosOpts, plan.Cfg.Runtime),
                AppleMobileMode.Native => new ObjcNativeEmitter(iosOpts, plan.Cfg.Runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(plan.IosMode), plan.IosMode, "Unknown AppleMobileMode")
            };
        }

        private static void AddTvosEmitter(Dictionary<string, IIrEmitter> emitters, EmitterPlan plan)
        {
            if (plan.Cfg.Targets.Tvos is not TvosTargetConfig { Enabled: true } tvosCfg)
                return;

            var tvosOpts = TvosEmitterOptions.FromConfig(tvosCfg);

            emitters["tvos"] = plan.TvosMode switch
            {
                AppleMobileMode.Objc => new ObjcEmitter(tvosOpts, plan.Cfg.Runtime),
                AppleMobileMode.Swift => new SwiftEmitter(tvosOpts, plan.Cfg.Runtime),
                AppleMobileMode.Native => new ObjcNativeEmitter(tvosOpts, plan.Cfg.Runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(plan.TvosMode), plan.TvosMode, "Unknown AppleMobileMode")
            };
        }

        private static void AddDocsEmitter(Dictionary<string, IIrEmitter> emitters, EmitterPlan plan)
        {
            if (plan.Cfg.Extras.Docs is not { Enabled: true } d) return;

            var docOpts = DocEmitterOptions.FromConfig(d);
            emitters["docs"] = new DocEmitter(docOpts, plan.Cfg.Runtime);
        }

        private static int InitProject(string folder)
        {
            try
            {
                var outDir = Path.GetFullPath(folder);
                Directory.CreateDirectory(outDir);

                var schemaFileName = "extgen.schema.json";
                var configFileName = "config.json";

                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

                var schemaPath = Path.Combine(outDir, schemaFileName);
                var configPath = Path.Combine(outDir, configFileName);

                JsonNode schema = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(ExtGenConfig));
                File.WriteAllText(schemaPath, schema.ToString(), encoding);

                // 2) Create default config
                var cfg = new ExtGenConfig
                {
                    Schema = $"./{schemaFileName}",
                    Root = "./"
                };

                var json = JsonSerializer.Serialize(cfg, jsonSerializerOptions);
                File.WriteAllText(configPath, json, encoding);

                Console.WriteLine($"[extgen] Wrote: {configPath}");
                Console.WriteLine($"[extgen] Wrote: {schemaPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 98;
            }
        }

        private static bool EnsureSchemaBesideConfigAndPatchConfigJson(string fullConfigPath, string schemaFileName = "extgen.schema.json")
        {
            var cfgDir = Path.GetDirectoryName(fullConfigPath)!;
            Directory.CreateDirectory(cfgDir);

            var schemaPath = Path.Combine(cfgDir, schemaFileName);
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            // 1) Write schema file
            JsonNode schema = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(ExtGenConfig));
            File.WriteAllText(schemaPath, schema.ToString(), encoding);

            // 2) Patch config.json to include/overwrite $schema
            var raw = File.ReadAllText(fullConfigPath, Encoding.UTF8);

            // Use JsonNode to preserve unknown properties
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(raw, new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = false
                });
            }
            catch (JsonException)
            {
                // If config is invalid JSON, still emit schema but don't modify config
                throw;
            }

            if (node is not JsonObject obj)
                throw new JsonException("Config root must be a JSON object.");

            var desired = $"./{schemaFileName}";
            var current = obj["$schema"]?.GetValue<string>();

            if (!string.Equals(current, desired, StringComparison.Ordinal))
            {
                obj["$schema"] = desired;

                // Re-emit with indentation (you *will* lose comments; JSON can't keep them)
                var patched = obj.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(fullConfigPath, patched, encoding);
                return false;
            }

            return true;
        }
    }
}