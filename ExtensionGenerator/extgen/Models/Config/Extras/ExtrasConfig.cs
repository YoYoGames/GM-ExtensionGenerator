using System.Text.Json.Serialization;

namespace extgen.Models.Config.Extras
{
    // ============================================================
    // Extras (never required; purely optional)
    // ============================================================

    public sealed class ExtrasConfig
    {
        [JsonPropertyName("docs")] public DocsConfig? Docs { get; set; }
    }
}
