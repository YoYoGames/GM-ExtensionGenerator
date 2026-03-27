using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Consoles
{
    // ============================================================
    // Switch target
    // ============================================================

    public sealed class SwitchTargetConfig : GeneratorConfigBase
    {
        /// <summary>
        /// Switch builds commonly depend on importing a user-provided MSBuild .props.
        /// You should never ship the props; users point to it.
        /// </summary>
        [JsonPropertyName("vcTargetsPath")]
        public string? VCTargetsPath { get; set; }

        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../";
    }
}
