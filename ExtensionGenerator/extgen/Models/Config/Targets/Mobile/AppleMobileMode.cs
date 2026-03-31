using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    /// <summary>
    /// Apple mobile platform code generation mode (iOS/tvOS).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AppleMobileMode
    {
        /// <summary>Generate Objective-C bindings.</summary>
        [JsonStringEnumMemberName("objc")]
        Objc,

        /// <summary>Generate Swift bindings.</summary>
        [JsonStringEnumMemberName("swift")]
        Swift,

        /// <summary>Generate native C/C++ bindings.</summary>
        [JsonStringEnumMemberName("native")]
        Native
    }
}
