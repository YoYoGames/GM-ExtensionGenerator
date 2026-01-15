using System.Text.Json.Serialization;

namespace extgen.Options
{
    public sealed class JniEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputJavaFolder")]
        public string OutputJavaFolder { get; set; } = "../AndroidSource/Java";

        [JsonPropertyName("outputNativeFolder")]
        public string OutputNativeFolder { get; set; } = "./code_gen/android";

        [JsonPropertyName("outputBinaryFolder")]
        public string OutputBinaryFolder { get; set; } = "../AndroidSource/libs";

    }
}
