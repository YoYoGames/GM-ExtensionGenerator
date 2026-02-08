using extgen.Models.Config.Targets.Consoles;
using extgen.Models.Config.Targets.Desktop;
using extgen.Models.Config.Targets.Mobile;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets
{
    // ============================================================
    // Targets container
    // ============================================================

    public sealed class TargetsConfig
    {
        [JsonPropertyName("sourceFolder")] public string SourceFolder { get; set; } = "./native";
        [JsonPropertyName("sourceFile")] public string SourceFilename { get; set; } = "{0}_native";

        [JsonPropertyName("windows")] public WindowsTargetConfig? Windows { get; set; }
        [JsonPropertyName("macos")] public MacTargetConfig? MacOS { get; set; }
        [JsonPropertyName("linux")] public LinuxTargetConfig? Linux { get; set; }

        [JsonPropertyName("android")] public AndroidTargetConfig? Android { get; set; }

        [JsonPropertyName("ios")] public IosTargetConfig? Ios { get; set; }
        [JsonPropertyName("tvos")] public TvosTargetConfig? Tvos { get; set; }

        [JsonPropertyName("xbox")] public XboxTargetConfig? Xbox { get; set; }
        [JsonPropertyName("ps4")] public Ps4TargetConfig? Ps4 { get; set; }
        [JsonPropertyName("ps5")] public Ps5TargetConfig? Ps5 { get; set; }
        [JsonPropertyName("switch")] public SwitchTargetConfig? Switch { get; set; }
    }
}
