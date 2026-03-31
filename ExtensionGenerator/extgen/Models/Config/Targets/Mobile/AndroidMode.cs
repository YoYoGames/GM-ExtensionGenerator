using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    /// <summary>
    /// Android code generation mode.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AndroidMode
    {
        /// <summary>Generate Java bindings.</summary>
        [JsonStringEnumMemberName("java")]
        Java,

        /// <summary>Generate Kotlin bindings.</summary>
        [JsonStringEnumMemberName("kotlin")]
        Kotlin,

        /// <summary>Generate JNI bindings.</summary>
        [JsonStringEnumMemberName("jni")]
        Jni
    }
}
