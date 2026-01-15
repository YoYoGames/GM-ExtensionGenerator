using System.Text.Json.Serialization;

namespace extgen.Options
{
    public interface IObjcEmitterOptions 
    {
        public bool Enabled { get; set; }

        public string Platform { get; }
    }

    public sealed class IosEmitterOptions : IObjcEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        // not configurable from JSON
        public string Platform => "ios";
    }

    public sealed class TvosEmitterOptions : IObjcEmitterOptions
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

        // not configurable from JSON
        public string Platform => "tvos";
    }
}
