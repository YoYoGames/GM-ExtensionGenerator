using extgen.Models.Config;
using System.Text;
using System.Text.Json;

namespace extgen.Config
{
    /// <summary>
    /// Implements: --init <folder>
    /// Creates config.json + schema file into that folder.
    /// </summary>
    public sealed class ProjectInitializer(ConfigSchemaService schema, JsonSerializerOptions jsonOptions)
    {
        private readonly ConfigSchemaService _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        private readonly JsonSerializerOptions _jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));

        public int Init(string folder, string configFileName = "config.json", string schemaFileName = "extgen.schema.json")
        {
            try
            {
                var outDir = Path.GetFullPath(folder);
                Directory.CreateDirectory(outDir);

                var schemaPath = Path.Combine(outDir, schemaFileName);
                var configPath = Path.Combine(outDir, configFileName);

                // 1) Write schema
                _ = _schema.WriteSchemaBesideConfig<ExtGenConfig>(configPath, schemaFileName);

                // 2) Default config
                var cfg = new ExtGenConfig
                {
                    Schema = $"./{schemaFileName}",
                    Root = "./"
                };

                var json = JsonSerializer.Serialize(cfg, _jsonOptions);
                File.WriteAllText(configPath, json, new UTF8Encoding(false));

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
    }
}
