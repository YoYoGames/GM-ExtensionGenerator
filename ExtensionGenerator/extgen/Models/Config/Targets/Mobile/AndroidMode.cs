using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AndroidMode
    {
        [JsonStringEnumMemberName("java")] 
        Java,
        [JsonStringEnumMemberName("kotlin")]
        Kotlin,
        [JsonStringEnumMemberName("jni")] 
        Jni
    }
}
