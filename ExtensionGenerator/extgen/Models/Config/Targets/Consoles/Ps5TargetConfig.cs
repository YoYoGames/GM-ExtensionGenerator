using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Consoles
{
    /// <summary>
    /// PlayStation 5 platform configuration.
    /// </summary>
    public sealed class Ps5TargetConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../";
    }
}
