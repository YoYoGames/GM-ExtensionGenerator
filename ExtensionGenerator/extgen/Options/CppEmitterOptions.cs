using System.Text.Json.Serialization;

namespace extgen.Options
{
    public sealed class CppEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputBinaryFolder")]
        public string OutputBinaryFolder { get; set; } = "../";

        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "native";

        [JsonPropertyName("sourceFilename")]
        public string SourceFilename { get; set; } = "{0}_native";
    }
}
