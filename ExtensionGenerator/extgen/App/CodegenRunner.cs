using extgen.Config;
using extgen.Emitters.Cmake;
using extgen.Mappers;
using extgen.Models;
using extgen.Models.Config;
using extgen.Parsing.Gmidl;
using extgen.Planning;
using System.Text;
using System.Text.Json;

namespace extgen.App
{
    public sealed class CodegenRunner
    {
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConfigSchemaService _schema;

        public CodegenRunner(JsonSerializerOptions jsonOptions, ConfigSchemaService schema)
        {
            _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public int RunFromConfig(string configPath)
        {
            var fullConfigPath = Path.GetFullPath(configPath);
            if (!File.Exists(fullConfigPath))
            {
                Console.Error.WriteLine($"Config file not found: {fullConfigPath}");
                return 3;
            }

            // Always emit schema + patch $schema (but continue running)
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
                rc = ConfigResolver.Resolve(cfg, fullConfigPath, ResolvePath);
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

            // Build emitters
            var emitters = EmitterBuilder.Build(rc);

            // CMake emission is typically build-driven; you can gate it by rc.AllowBuild if you want.
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

        private static string ResolvePath(string? path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var expanded = Environment.ExpandEnvironmentVariables(
                path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(baseDir, expanded));
        }
    }
}
