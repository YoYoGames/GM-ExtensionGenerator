using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    public sealed class CmakeEmitterOptions
    {
        /// <summary>Default C++ standard to put into templates/presets (e.g. 17, 20).</summary>
        [JsonPropertyName("cppStandard")]
        public int CppStandard { get; set; } = 17;

        /// <summary>Whether to enable compiler extensions (CMAKE_CXX_EXTENSIONS).</summary>
        [JsonPropertyName("cppExtensions")]
        public bool CppExtensions { get; set; } = false;

        /// <summary>Emit strict warnings flags by default.</summary>
        [JsonPropertyName("strictWarnings")]
        public bool StrictWarnings { get; set; } = true;

        /// <summary>Enable third_party directory integration by default.</summary>
        [JsonPropertyName("useThirdParty")]
        public bool UseThirdParty { get; set; } = true;

        /// <summary>Emit CMakePresets.json.</summary>
        [JsonPropertyName("emitPresets")]
        public bool EmitPresets { get; set; } = true;
    }
}
