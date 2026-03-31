using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    /// <summary>
    /// Configuration for Apple platform (iOS/tvOS) build and packaging settings.
    /// </summary>
    public sealed class AppleBuildConfig
    {
        /// <summary>Default packaging config used by xcframework scripts.</summary>
        [JsonPropertyName("packageConfig")]
        public string PackageConfig { get; set; } = "Release";

        /// <summary>If true, build simulator slice when packaging xcframework.</summary>
        [JsonPropertyName("buildSimulator")]
        public bool BuildSimulator { get; set; } = true;

        /// <summary>
        /// Apple simulator architecture selection. Swift with multiple architectures may require additional configuration.
        /// </summary>
        [JsonPropertyName("simArm64")] public bool SimArm64 { get; set; } = true;
        [JsonPropertyName("simX64")] public bool SimX64 { get; set; } = false;

        /// <summary>Device arch selection for packaging.</summary>
        [JsonPropertyName("deviceArm64")] public bool DeviceArm64 { get; set; } = true;

        /// <summary>Optional deployment target (e.g. 11.0).</summary>
        [JsonPropertyName("deploymentTarget")]
        public string DeploymentTarget { get; set; } = "11.0";
    }
}
