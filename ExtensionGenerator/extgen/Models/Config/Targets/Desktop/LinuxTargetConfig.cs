using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Desktop
{
    public sealed class LinuxTargetConfig : GeneratorConfigBase
    {
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../";
    }
}
