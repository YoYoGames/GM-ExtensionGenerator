using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace extgen.Config
{
    /// <summary>
    /// Writes JSON schema beside config, and patches config.json "$schema" field.
    /// </summary>
    public sealed class ConfigSchemaService
    {
        private readonly JsonSerializerOptions _schemaOptions;
        private readonly Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public ConfigSchemaService(JsonSerializerOptions schemaOptions)
        {
            _schemaOptions = schemaOptions ?? throw new ArgumentNullException(nameof(schemaOptions));
        }

        public string DefaultSchemaFileName { get; init; } = "extgen.schema.json";

        /// <summary>
        /// Writes schema file beside config, returns schemaPath.
        /// </summary>
        public string WriteSchemaBesideConfig<TConfig>(string fullConfigPath, string? schemaFileName = null)
        {
            if (string.IsNullOrWhiteSpace(fullConfigPath))
                throw new ArgumentException("Config path is empty.", nameof(fullConfigPath));

            var cfgDir = Path.GetDirectoryName(Path.GetFullPath(fullConfigPath))!;
            Directory.CreateDirectory(cfgDir);

            var schemaName = string.IsNullOrWhiteSpace(schemaFileName) ? DefaultSchemaFileName : schemaFileName!;
            var schemaPath = Path.Combine(cfgDir, schemaName);

            JsonNode schema = JsonSerializerOptions.Default.GetJsonSchemaAsNode(typeof(TConfig));
            File.WriteAllText(schemaPath, schema.ToString(), _utf8NoBom);

            return schemaPath;
        }

        /// <summary>
        /// Writes schema, and patches config's "$schema" to "./{schemaFileName}".
        /// Returns true if config JSON was modified, false if already correct.
        /// </summary>
        public bool EnsureSchemaBesideConfigAndPatchConfigJson<TConfig>(string fullConfigPath, string? schemaFileName = null)
        {
            var schemaName = string.IsNullOrWhiteSpace(schemaFileName) ? DefaultSchemaFileName : schemaFileName!;
            _ = WriteSchemaBesideConfig<TConfig>(fullConfigPath, schemaName);

            // Patch config.json to include/overwrite $schema
            var raw = File.ReadAllText(fullConfigPath, Encoding.UTF8);

            JsonNode node = JsonNode.Parse(raw, new JsonNodeOptions { PropertyNameCaseInsensitive = false })
                          ?? throw new JsonException("Config JSON parsed to null.");

            if (node is not JsonObject obj)
                throw new JsonException("Config root must be a JSON object.");

            var desired = $"./{schemaName}";
            var current = obj["$schema"]?.GetValue<string>();

            if (!string.Equals(current, desired, StringComparison.Ordinal))
            {
                obj["$schema"] = desired;

                // NOTE: JSON can't preserve comments.
                var patched = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(fullConfigPath, patched, _utf8NoBom);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates a default config file (does NOT patch an existing one).
        /// Caller chooses what defaults to include.
        /// </summary>
        public void WriteDefaultConfig<TConfig>(string fullConfigPath, TConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg, _schemaOptions);
            File.WriteAllText(fullConfigPath, json, _utf8NoBom);
        }
    }
}
