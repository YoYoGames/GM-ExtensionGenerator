using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    // ============================================================
    // Build config (CMake + presets + packaging knobs)
    // ============================================================

    public sealed class BuildConfig
    {
        /// <summary>If false, do not emit any CMake build files/presets.</summary>
        [JsonPropertyName("emitCmake")] public bool EmitCmake { get; set; } = true;

        [JsonPropertyName("cmake")] public CmakeConfig Cmake { get; set; } = new();

        /// <summary>Apple packaging knobs for xcframework generation.</summary>
        //[JsonPropertyName("apple")] public AppleBuildOptions Apple { get; set; } = new();

        /// <summary>Console build knobs, SDK hints, and preset emission settings.</summary>
        // [JsonPropertyName("consoles")] public ConsoleBuildOptions Consoles { get; set; } = new();
    }
}
