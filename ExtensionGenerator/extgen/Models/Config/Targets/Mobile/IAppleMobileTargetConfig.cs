using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    // ============================================================
    // Apple Mobile targets (iOS/tvOS)
    // ============================================================

    public interface IAppleMobileTargetConfig : IGeneratorConfig
    {
        [JsonPropertyName("mode"), JsonRequired()]
        public AppleMobileMode Mode { get; set; }
    }
}
