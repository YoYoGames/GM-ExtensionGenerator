using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    /// <summary>
    /// iOS platform configuration.
    /// </summary>
    public class IosTargetConfig : GeneratorConfigBase, IAppleMobileTargetConfig
    {
        /// <inheritdoc />
        [JsonPropertyName("mode"), JsonRequired()]
        public AppleMobileMode Mode { get; set; } = AppleMobileMode.Objc;

        /// <summary>Source folder for platform-specific native code.</summary>
        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "./ios";

        /// <summary>Source filename format string.</summary>
        [JsonPropertyName("sourceFilename")]
        public string SourceFilename { get; set; } = "{0}_ios";

        /// <inheritdoc />
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../iOSSourceFromMac";

        /// <summary>Output folder for additional source files.</summary>
        [JsonPropertyName("outputSourceFolder")]
        public string OutputSource { get; set; } = "../iOSSource";
    }
}
