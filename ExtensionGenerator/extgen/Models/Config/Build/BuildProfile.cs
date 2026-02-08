using System.Text.Json.Serialization;

namespace extgen.Models.Config.Build
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BuildProfile
    {
        /// <summary>Generate a full GameMaker extension package (code + build + optional extras).</summary>
        Full,

        /// <summary>Generate only bindings / bridging code; no build system / presets unless explicitly enabled.</summary>
        BindingsOnly,

        /// <summary>Generate only build outputs (CMakeLists/presets/packaging); assumes sources already exist.</summary>
        BuildOnly
    }
}
