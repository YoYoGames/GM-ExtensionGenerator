using extgen.Models.Config.Targets.Consoles;
using extgen.Models.Config.Targets.Desktop;
using extgen.Models.Config.Targets.Mobile;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets
{
    /// <summary>
    /// Platform target configuration container.
    /// </summary>
    public sealed class TargetsConfig
    {
        /// <summary>Folder containing native source files.</summary>
        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "./native";

        /// <summary>Format string for native source filenames (e.g. "{0}_native").</summary>
        [JsonPropertyName("sourceFile")]
        public string SourceFilename { get; set; } = "{0}_native";

        /// <summary>Windows desktop platform configuration.</summary>
        [JsonPropertyName("windows")]
        public WindowsTargetConfig? Windows { get; set; }

        /// <summary>macOS desktop platform configuration.</summary>
        [JsonPropertyName("macos")]
        public MacTargetConfig? MacOS { get; set; }

        /// <summary>Linux desktop platform configuration.</summary>
        [JsonPropertyName("linux")]
        public LinuxTargetConfig? Linux { get; set; }

        /// <summary>Android mobile platform configuration.</summary>
        [JsonPropertyName("android")]
        public AndroidTargetConfig? Android { get; set; }

        /// <summary>iOS mobile platform configuration.</summary>
        [JsonPropertyName("ios")]
        public IosTargetConfig? Ios { get; set; }

        /// <summary>tvOS mobile platform configuration.</summary>
        [JsonPropertyName("tvos")]
        public TvosTargetConfig? Tvos { get; set; }

        /// <summary>Xbox console platform configuration.</summary>
        [JsonPropertyName("xbox")]
        public XboxTargetConfig? Xbox { get; set; }

        /// <summary>PlayStation 4 console platform configuration.</summary>
        [JsonPropertyName("ps4")]
        public Ps4TargetConfig? Ps4 { get; set; }

        /// <summary>PlayStation 5 console platform configuration.</summary>
        [JsonPropertyName("ps5")]
        public Ps5TargetConfig? Ps5 { get; set; }

        /// <summary>Nintendo Switch console platform configuration.</summary>
        [JsonPropertyName("switch")]
        public SwitchTargetConfig? Switch { get; set; }
    }
}
