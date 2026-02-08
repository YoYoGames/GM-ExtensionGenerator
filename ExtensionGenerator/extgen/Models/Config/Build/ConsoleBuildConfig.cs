using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    public sealed class ConsoleBuildConfig
    {
        /// <summary>
        /// If true, emit Visual Studio generator presets for consoles by default (recommended).
        /// </summary>
        [JsonPropertyName("preferVisualStudio")]
        public bool PreferVisualStudio { get; set; } = true;

        /// <summary>VS version string to use in presets (e.g. "Visual Studio 17 2022").</summary>
        [JsonPropertyName("vsGenerator")]
        public string VsGenerator { get; set; } = "Visual Studio 17 2022";

        /// <summary>Default VS toolset to target (e.g. v143).</summary>
        [JsonPropertyName("vsToolset")]
        public string VsToolset { get; set; } = "v143";
    }
}
