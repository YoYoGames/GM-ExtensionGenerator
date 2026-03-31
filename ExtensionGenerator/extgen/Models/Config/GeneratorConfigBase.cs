using System.Text.Json.Serialization;

namespace extgen.Models.Config
{
    /// <summary>
    /// Base class for platform target configuration.
    /// </summary>
    public abstract class GeneratorConfigBase : IGeneratorConfig
    {
        /// <inheritdoc />
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>Output folder for generated files.</summary>
        [JsonPropertyName("outputFolder")]
        public abstract string Output { get; set; }
    }
}
