using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    /// <summary>
    /// Build system configuration for CMake, presets, and packaging.
    /// </summary>
    public sealed class BuildConfig
    {
        /// <summary>If false, do not emit any CMake build files/presets.</summary>
        [JsonPropertyName("emitCmake")]
        public bool EmitCmake { get; set; } = true;

        /// <summary>CMake-specific configuration.</summary>
        [JsonPropertyName("cmake")]
        public CmakeConfig Cmake { get; set; } = new();
    }
}
