using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    // ============================================================
    // Android target
    // ============================================================

    public sealed class AndroidTargetConfig : GeneratorConfigBase
    {
        [JsonPropertyName("mode"), JsonRequired()]
        public AndroidMode Mode { get; set; } = AndroidMode.Java;
        [JsonPropertyName("outputFolder")]
        public override string OutputFolder { get; set; } = "../AndroidSource";
    }
}
