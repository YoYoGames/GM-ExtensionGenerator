using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Consoles
{
    // ============================================================
    // PlayStation targets
    // ============================================================

    public sealed class Ps4TargetConfig : GeneratorConfigBase
    {
        [JsonPropertyName("outputFolder")]
        public override string OutputFolder { get; set; } = "../";
    }
}
