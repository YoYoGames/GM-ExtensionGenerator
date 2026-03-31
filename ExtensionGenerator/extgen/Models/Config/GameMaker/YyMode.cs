using System.Text.Json.Serialization;

namespace extgen.Models.Config.GameMaker
{
    /// <summary>
    /// Mode for .yy file generation.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum YyMode
    {
        /// <summary>Generate plain .yy files from scratch.</summary>
        [JsonStringEnumMemberName("plain")]
        Plain,

        /// <summary>Patch existing .yy files.</summary>
        [JsonStringEnumMemberName("patch")]
        Patch
    }
}
