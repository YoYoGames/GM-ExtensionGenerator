using System.Text.Json.Serialization;

namespace extgen.Options
{
    public sealed class CppEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputBinaryFolder")]
        public string OutputBinaryFolder { get; set; } = "../";

        [JsonPropertyName("userImplFolder")]
        public string UserImplOutputFolder { get; set; } = "native";

        [JsonPropertyName("userImplNameFormat")]
        public string UserImplOutputName { get; set; } = "{0}_native";
    }
}
