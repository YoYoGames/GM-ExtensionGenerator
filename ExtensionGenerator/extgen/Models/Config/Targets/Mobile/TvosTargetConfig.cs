using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    public class TvosTargetConfig : GeneratorConfigBase, IAppleMobileTargetConfig
    {
        [JsonPropertyName("mode"), JsonRequired()]
        public AppleMobileMode Mode { get; set; } = AppleMobileMode.Objc;
        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "./tvos";
        [JsonPropertyName("sourceFilename")]
        public string SourceFilename { get; set; } = "{0}_tvos";
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../tvOSSourceFromMac";
    }
}
