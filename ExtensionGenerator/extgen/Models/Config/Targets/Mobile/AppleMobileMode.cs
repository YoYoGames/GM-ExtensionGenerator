using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    // ============================================================
    // Enums
    // ============================================================

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AppleMobileMode
    {
        [JsonStringEnumMemberName("objc")] Objc,
        [JsonStringEnumMemberName("swift")] Swift,
        [JsonStringEnumMemberName("native")] Native
    }
}
