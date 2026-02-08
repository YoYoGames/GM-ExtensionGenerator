using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Desktop
{
    // ============================================================
    // Desktop targets
    // ============================================================

    public sealed class WindowsTargetConfig : GeneratorConfigBase
    {
        [JsonPropertyName("outputFolder")]
        public override string OutputFolder { get; set; } = "../";
    }
}
