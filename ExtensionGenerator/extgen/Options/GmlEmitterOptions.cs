using System.Text.Json.Serialization;

namespace extgen.Options
{
    public sealed class GmlEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputFile")]
        public string OutputFile { get; set; } = "./codegen.gml";

        [JsonPropertyName("outputCoreFile")]
        public string? OutputCoreFile { get; set; }
    }
}
