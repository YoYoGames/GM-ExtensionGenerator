using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    /// <summary>
    /// Build system configuration for CMake, presets, and packaging.
    /// </summary>
    public sealed class BuildConfig
    {
        /// <summary>
        /// Native implementation language. Default is C++ (existing behavior).
        /// When set to Rust, Cargo is used instead of CMake and Rust emitters run.
        /// </summary>
        [JsonPropertyName("nativeBackend")]
        public NativeBackend NativeBackend { get; set; } = NativeBackend.Cpp;

        /// <summary>If false, do not emit any CMake build files/presets (Cpp backend only).</summary>
        [JsonPropertyName("emitCmake")]
        public bool EmitCmake { get; set; } = true;

        /// <summary>CMake-specific configuration.</summary>
        [JsonPropertyName("cmake")]
        public CmakeConfig Cmake { get; set; } = new();
    }
}
