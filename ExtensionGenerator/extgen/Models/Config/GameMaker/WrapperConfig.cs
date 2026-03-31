using System.Text.Json.Serialization;

namespace extgen.Models.Config.GameMaker
{
    public sealed class WrapperConfig : IGeneratorConfig
    {

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("outputFile")]
        public string Output { get; set; } = "./api.gml";
    }
}
