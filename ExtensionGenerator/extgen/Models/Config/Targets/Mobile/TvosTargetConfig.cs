using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    /// <summary>
    /// tvOS platform configuration.
    /// </summary>
    public class TvosTargetConfig : GeneratorConfigBase, IAppleMobileTargetConfig
    {
        /// <inheritdoc />
        [JsonPropertyName("mode"), JsonRequired()]
        public AppleMobileMode Mode { get; set; } = AppleMobileMode.Objc;

        /// <summary>Source folder for platform-specific native code.</summary>
        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "./tvos";

        /// <summary>Source filename format string.</summary>
        [JsonPropertyName("sourceFilename")]
        public string SourceFilename { get; set; } = "{0}_tvos";

        /// <inheritdoc />
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../tvOSSourceFromMac";

        /// <summary>Output folder for additional source files.</summary>
        [JsonPropertyName("outputSourceFolder")]
        public string OutputSource { get; set; } = "../tvOSSource";
    }
}
