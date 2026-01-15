using System.Text.Json.Serialization;

namespace extgen.Options
{
    public class CmakeEmitterOptions
    {
        [JsonPropertyName("cppVersion")]
        public string CppVersion { get; set; } = "20";

        [JsonPropertyName("cppExtensions")]
        public bool CppExtensions { get; set; } = false;

        [JsonPropertyName("useThirdParty")]
        public bool UseThirdParty { get; set; } = true;

        [JsonPropertyName("strictWarnings")]
        public bool StrictWarnings { get; set; } = true;

    }
}