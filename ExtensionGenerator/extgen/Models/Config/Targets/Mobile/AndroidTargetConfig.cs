using extgen.Models.Config;
using System.Text.Json.Serialization;

namespace extgen.Models.Config.Targets.Mobile
{
    /// <summary>
    /// Android platform configuration.
    /// </summary>
    public sealed class AndroidTargetConfig : GeneratorConfigBase
    {
        /// <summary>Code generation mode (Java, Kotlin, or JNI).</summary>
        [JsonPropertyName("mode"), JsonRequired()]
        public AndroidMode Mode { get; set; } = AndroidMode.Java;

        /// <inheritdoc />
        [JsonPropertyName("outputFolder")]
        public override string Output { get; set; } = "../AndroidSource";
    }
}
