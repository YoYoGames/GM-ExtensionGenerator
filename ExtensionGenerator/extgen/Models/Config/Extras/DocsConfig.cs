using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Extras
{
    public sealed class DocsConfig : GeneratorConfigBase
    {
        [JsonPropertyName("outputFolder")]
        public override string OutputFolder { get; set; } = "./";

        [JsonPropertyName("outputFileName")] public string OutputFileName { get; set; } = "documentation";

        /// <summary>If true, overwrite existing files. If false, try to be additive.</summary>
        [JsonPropertyName("overwrite")] public bool Overwrite { get; set; } = true;
    }
}
