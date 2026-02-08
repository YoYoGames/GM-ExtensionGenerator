using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Consoles
{
    public sealed class Ps5TargetConfig : GeneratorConfigBase
    {
        [JsonPropertyName("outputFolder")]
        public override string OutputFolder { get; set; } = "../";
    }
}
