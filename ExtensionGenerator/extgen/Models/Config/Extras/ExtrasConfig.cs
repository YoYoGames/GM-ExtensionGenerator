using System.Text.Json.Serialization;

namespace extgen.Models.Config.Extras
{
    /// <summary>
    /// Optional extra features configuration (documentation, etc.).
    /// </summary>
    public sealed class ExtrasConfig
    {
        /// <summary>Documentation generation configuration.</summary>
        [JsonPropertyName("docs")]
        public DocsConfig? Docs { get; set; }
    }
}
