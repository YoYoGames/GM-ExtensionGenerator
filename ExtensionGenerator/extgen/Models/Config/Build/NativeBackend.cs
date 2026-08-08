using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    /// <summary>
    /// Native implementation language for desktop/JNI/native-Apple targets.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NativeBackend
    {
        /// <summary>Generate C++ native sources and use CMake (default).</summary>
        [JsonStringEnumMemberName("cpp")]
        Cpp,

        /// <summary>Generate Rust native sources and use Cargo.</summary>
        [JsonStringEnumMemberName("rust")]
        Rust
    }
}
