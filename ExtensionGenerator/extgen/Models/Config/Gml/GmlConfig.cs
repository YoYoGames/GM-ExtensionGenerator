using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Gml
{
    // ============================================================
    // Runtime (gml code generation always active)
    // ============================================================

    public sealed class GmlConfig : IGeneratorConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("emitRuntime")]
        public bool EmitRuntime { get; set; } = false;

        [JsonPropertyName("outputFile")]
        public string OutputFile { get; set; } = "./api.gml";

        [JsonPropertyName("declarationsFile")]
        public string DeclarationsFile { get; set; } = "./declarations.yy";

        [JsonPropertyName("runtimeFilename")]
        public string RuntimeFile { get; set; } = "./runtime.gml";
    }
}
