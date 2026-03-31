using System.Text.Json.Serialization;

namespace extgen.Models.Config
{
    /// <summary>
    /// Base interface for platform target configuration.
    /// </summary>
    public interface IGeneratorConfig
    {
        /// <summary>Whether code generation is enabled for this target.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
