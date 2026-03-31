using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Consoles
{
    /// <summary>
    /// PlayStation 4 platform configuration.
    /// </summary>
    public sealed class Ps4TargetConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../";
    }
}
