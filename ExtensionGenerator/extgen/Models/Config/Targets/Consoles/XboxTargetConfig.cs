using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Consoles
{
    /// <summary>
    /// Xbox platform configuration (GDK).
    /// </summary>
    public sealed class XboxTargetConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../";
    }
}
