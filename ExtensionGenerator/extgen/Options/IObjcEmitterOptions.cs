using System.Text.Json.Serialization;

namespace extgen.Options
{
    public interface IObjcEmitterOptions 
    {
        public bool Enabled { get; set; }

        public string Platform { get; }

        public string SourceFolder { get; set; }

        public string SourceFilename { get; set; }
    }

    public sealed class IosEmitterOptions : IObjcEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        // not configurable from JSON
        public string Platform => "ios";

        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "./ios";

        [JsonPropertyName("sourceFilename")]
        public string SourceFilename { get; set; } = "{0}_ios";
    }

    public sealed class TvosEmitterOptions : IObjcEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        // not configurable from JSON
        public string Platform => "tvos";

        [JsonPropertyName("sourceFolder")]
        public string SourceFolder { get; set; } = "./tvos";

        [JsonPropertyName("sourceFilename")]
        public string SourceFilename { get; set; } = "{0}_tvos";
    }
}
