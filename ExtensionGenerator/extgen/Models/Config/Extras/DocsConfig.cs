using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Extras
{
    public sealed class DocsConfig : GeneratorConfigBase
    {
        [JsonPropertyName("outputFile")]
        public override string Output { get; set; } = "./extgen_docs.js";

        /// <summary>If true, overwrite existing files. If false, try to be additive.</summary>
        [JsonPropertyName("overwrite")] public bool Overwrite { get; set; } = true;
    }
}
