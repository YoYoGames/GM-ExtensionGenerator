using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    /// <summary>
    /// Configuration interface for Apple mobile platforms (iOS/tvOS).
    /// </summary>
    public interface IAppleMobileTargetConfig : IGeneratorConfig
    {
        /// <summary>Code generation mode for Apple mobile platforms.</summary>
        [JsonPropertyName("mode"), JsonRequired()]
        public AppleMobileMode Mode { get; set; }
    }
}
