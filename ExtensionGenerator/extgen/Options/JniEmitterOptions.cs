using System.Text.Json.Serialization;

namespace extgen.Options
{
    public sealed class JniEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputJavaFolder")]
        public string OutputJavaFolder { get; set; } = "../AndroidSource/Java";

        [JsonPropertyName("outputBinaryFolder")]
        public string OutputBinaryFolder { get; set; } = "../AndroidSource/libs";

        public string OutputNativeFolder => "./code_gen/android";
    }
}
