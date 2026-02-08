using System.Text.Json.Serialization;

namespace extgen.Models.Config
{
    public interface IGeneratorConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
