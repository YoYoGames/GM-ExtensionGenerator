using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Extras
{
    /// <summary>
    /// Documentation generation configuration.
    /// </summary>
    public sealed class DocsConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFile")]
        public override string Output { get; set; } = "./extgen_docs.js";

        /// <summary>If true, overwrite existing files. If false, be additive.</summary>
        [JsonPropertyName("overwrite")]
        public bool Overwrite { get; set; } = true;
    }
}
