using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Desktop
{
    /// <summary>
    /// Linux desktop platform configuration.
    /// </summary>
    public sealed class LinuxTargetConfig : GeneratorConfigBase
    {
        /// <inheritdoc />
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../";
    }
}
