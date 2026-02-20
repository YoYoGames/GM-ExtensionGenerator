using extgen.Models.Config;
using System.Runtime;
using System.Text.Json;
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

        [JsonPropertyName("extensionFile")]
        public string ExtensionFile { get; set; } = "./extension.yy";

        [JsonPropertyName("patchFrameworks")]
        public bool PatchFrameworks { get; set; } = false;

        [JsonPropertyName("runtimeFilename")]
        public string RuntimeFile { get; set; } = "./runtime.gml";
    }
}
