using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    /// <summary>
    /// Optional Cargo / Apple framework knobs for <c>nativeBackend: "rust"</c>.
    /// Empty fields keep extgen defaults (<c>{extension}_Rust</c>, derived bundle id, etc.).
    /// </summary>
    public sealed class RustBuildConfig
    {
        /// <summary>
        /// Override Cargo package / cdylib stem (sanitized like extension name).
        /// Ignored when <c>rust/Cargo.toml</c> already exists (IfMissing keeps hand-maintained name).
        /// </summary>
        [JsonPropertyName("crateName")]
        public string? CrateName { get; set; }

        /// <summary>iOS/tvOS dynamic framework basename (default <c>{ExtensionName}_Rust</c>).</summary>
        [JsonPropertyName("iosFrameworkName")]
        public string? IosFrameworkName { get; set; }

        /// <summary>CFBundleIdentifier for the Apple framework (default <c>com.extgen.{slug}.rust</c>).</summary>
        [JsonPropertyName("iosBundleId")]
        public string? IosBundleId { get; set; }

        /// <summary>macOS deployment target for Cargo env / scripts (default <c>11.0</c>).</summary>
        [JsonPropertyName("macosMinVersion")]
        public string? MacosMinVersion { get; set; }

        /// <summary>iOS deployment target (default <c>13.0</c>).</summary>
        [JsonPropertyName("iosMinVersion")]
        public string? IosMinVersion { get; set; }

        /// <summary>tvOS deployment target (default <c>13.0</c>).</summary>
        [JsonPropertyName("tvosMinVersion")]
        public string? TvosMinVersion { get; set; }
    }
}
