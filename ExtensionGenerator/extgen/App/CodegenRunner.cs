using extgen.Config;
using extgen.Emitters.Cmake;
using extgen.Mappers;
using extgen.Models;
using extgen.Models.Config;
using extgen.Parsing.Gmidl;
using extgen.Planning;
using extgen.Utils;
using System.Text;
using System.Text.Json;

namespace extgen.App
{
    /// <summary>
    /// Orchestrates the code generation process from configuration to emitter execution.
    /// </summary>
    public sealed class CodegenRunner
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConfigSchemaService _schema;

        /// <summary>
        /// Initializes a new code generation runner with JSON serialization options and schema service.
        /// </summary>
        public CodegenRunner(JsonSerializerOptions jsonOptions, ConfigSchemaService schema)
        {
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        /// <summary>
        /// Runs the code generation pipeline using the specified configuration file.
        /// </summary>
        /// <param name="configPath">Path to the extgen configuration JSON file.</param>
        /// <returns>Exit code (0 for success, non-zero for errors).</returns>
        public int RunFromConfig(string configPath)
        {
            // Pipeline stages:
            // 1. Load config JSON
            // 2. Emit JSON schema beside config (for IDE autocomplete)
            // 3. Resolve paths and validate config
            // 4. Load GMIDL file → parse into IR compilation
            // 5. Emit CMake build system (runs first, needed by other targets)
            // 6. Emit each enabled target (GML, Swift, Java, etc.)

            var fullConfigPath = Path.GetFullPath(configPath);
            if (!File.Exists(fullConfigPath))
            {
                Console.Error.WriteLine($"Config file not found: {fullConfigPath}");
                return 3;
            }

            // Always emit schema file beside the config (e.g., extgen.config.json → extgen.schema.json).
            // This enables IDE autocomplete/validation. We also patch the config's $schema property
            // to point to this generated file. If the config is already valid, this is a no-op.
            try
            {
                var modified = _schema.EnsureSchemaBesideConfigAndPatchConfigJson<ExtGenConfig>(fullConfigPath);
                if (modified)
                    Console.WriteLine("[extgen] Updated config '$schema' to the latest schema.");
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

            ExtGenConfig cfg;
            try
            {
                var json = File.ReadAllText(fullConfigPath, Encoding.UTF8);
                cfg = JsonSerializer.Deserialize<ExtGenConfig>(json, _jsonOptions)
                      ?? throw new JsonException("Empty or invalid configuration.");
            }
            catch (JsonException je)
            {
                Console.Error.WriteLine($"Config JSON error: {je.Message}");
                return 11;
            }

            ResolvedConfig rc;
            try
            {
                rc = ConfigResolver.Resolve(cfg, fullConfigPath, PathUtils.ResolvePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 5;
            }

            IrCompilation compilation;
            try
            {
                compilation = GmidlSchemaLoader.LoadFromFile(rc.InputPath)
                              ?? throw new Exception("Failed to load IR compilation.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 6;
            }

            // Build emitters for each enabled target (GML, Android, iOS, etc.)
            var emitters = EmitterBuilder.Build(rc);

            // CMake runs first because other targets may reference build artifacts.
            // It generates CMakeLists.txt + presets for each platform (Win/Mac/Linux/Switch).
            // This must complete before platform-specific emitters run.
            try
            {
                var config = rc.Raw.Build.Cmake;
                var cmakeEmitter = new CmakeEmitter(config.ToSettings(), rc.Raw);
                cmakeEmitter.Emit(compilation, rc.OutputDir);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CMake] Failed: {ex}");
                return 20;
            }

            if (emitters.Count == 0)
            {
                Console.WriteLine("[extgen] No targets enabled in config. Nothing to generate.");
                return 0;
            }

            foreach (var (key, emitter) in emitters)
            {
                try
                {
                    Console.WriteLine($"[extgen] {key.ToUpperInvariant()} -> {rc.OutputDir}");
                    emitter.Emit(compilation, rc.OutputDir);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[{key}] Failed: {ex.Message}, Where: {ex.Source}::{ex.TargetSite}");
                    return 30;
                }
            }

            Console.WriteLine("[extgen] Success [x]");
            return 0;
        }
    }
}
