using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    public class IosTargetConfig : GeneratorConfigBase, IAppleMobileTargetConfig
    {
        [JsonPropertyName("mode"), JsonRequired()]
        public AppleMobileMode Mode { get; set; } = AppleMobileMode.Objc;
        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "./ios";
        [JsonPropertyName("sourceFilename")]
        public string SourceFilename { get; set; } = "{0}_ios";
        [JsonPropertyName("outputFolder")]
        public override string OutputFolder { get; set; } = "../iOSSourceFromMac";
    }
}
