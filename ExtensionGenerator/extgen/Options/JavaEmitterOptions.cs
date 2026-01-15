using System.Text.Json.Serialization;

namespace extgen.Options
{
    public class JavaEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputJavaFolder")]
        public string OutputJavaFolder { get; set; } = "../AndroidSource/Java";

    }
}
