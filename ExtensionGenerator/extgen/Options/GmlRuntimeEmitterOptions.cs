using System.Text.Json.Serialization;

namespace extgen.Options
{
    public class GmlRuntimeEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputFile")]
        public string OutputFile { get; set; } = "./ExtensionCore_runtime.gml";
    }
}