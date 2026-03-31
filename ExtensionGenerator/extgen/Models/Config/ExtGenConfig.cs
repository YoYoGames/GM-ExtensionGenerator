using extgen.Models.Config.Build;
using extgen.Models.Config.Extras;
using extgen.Models.Config.GameMaker;
using extgen.Models.Config.Targets;
using System.Text.Json.Serialization;

namespace extgen.Models.Config
{
    /// <summary>
    /// Root configuration for the extension generator.
    /// </summary>
    public sealed class ExtGenConfig
    {
        /// <summary>JSON schema URI for validation.</summary>
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        /// <summary>Input GMIDL schema file path.</summary>
        [JsonPropertyName("input")]
        public string? Input { get; set; }

        /// <summary>Root output directory for generated files.</summary>
        [JsonPropertyName("root")]
        public string? Root { get; set; }

        /// <summary>Build profile determining which files to generate.</summary>
        [JsonPropertyName("profile")]
        public BuildProfile Profile { get; set; } = BuildProfile.Full;

        /// <summary>GameMaker-specific configuration.</summary>
        [JsonPropertyName("gamemaker")]
        public GameMakerConfig GameMaker { get; set; } = new GameMakerConfig();

        /// <summary>Platform target configuration.</summary>
        [JsonPropertyName("targets")]
        public TargetsConfig Targets { get; set; } = new();

        /// <summary>Optional extras configuration (e.g., documentation).</summary>
        [JsonPropertyName("extras")]
        public ExtrasConfig Extras { get; set; } = new();

        /// <summary>Build system configuration (CMake, presets).</summary>
        [JsonPropertyName("build")]
        public BuildConfig Build { get; set; } = new();

        /// <summary>Runtime naming conventions for generated symbols.</summary>
        [JsonPropertyName("runtime"), JsonIgnore()]
        public RuntimeNaming Runtime { get; set; } = new();
    }
}
