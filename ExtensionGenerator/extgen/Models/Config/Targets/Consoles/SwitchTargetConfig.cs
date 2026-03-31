using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Consoles
{
    // ============================================================
    // Switch target
    // ============================================================

    public sealed class SwitchTargetConfig : GeneratorConfigBase
    {
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../";
    }
}
