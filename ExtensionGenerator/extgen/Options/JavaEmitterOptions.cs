using System.Text.Json.Serialization;

namespace extgen.Options
{
    public class JavaEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputFolder")]
        public string OutputFolder { get; set; } = "../AndroidSource/Java";
    }
}
