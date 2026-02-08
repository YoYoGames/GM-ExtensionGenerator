using System.Text.Json.Serialization;

namespace extgen.Models.Config
{
    public abstract class GeneratorConfigBase : IGeneratorConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputFolder")]
        public abstract string OutputFolder { get; set; }
    }
}
