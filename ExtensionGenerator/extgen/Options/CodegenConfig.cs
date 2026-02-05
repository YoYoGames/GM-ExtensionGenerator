using extgen.Emitters.Doc;
using System.Text.Json.Serialization;

namespace extgen.Options
{
    public sealed class CodegenConfig
    {
        [JsonPropertyName("input")]
        public string? Input { get; set; }

        [JsonPropertyName("outputDir")]
        public string? OutputDir { get; set; }

        [JsonPropertyName("targets")]
        public List<string> Targets { get; set; } = [];

        [JsonPropertyName("cmake")]
        public CmakeEmitterOptions Cmake { get; set; } = new();

        public RuntimeNaming Runtime { get; } = new();

        // Per-target blocks (null = don’t generate)
        [JsonPropertyName("cpp")] public CppEmitterOptions? Cpp { get; set; }
        [JsonPropertyName("gml")] public GmlEmitterOptions? Gml { get; set; }
        [JsonPropertyName("gml_runtime")] public GmlRuntimeEmitterOptions? GmlRuntime { get; set; }
        [JsonPropertyName("yy")] public YyEmitterOptions? Yy { get; set; }

        [JsonPropertyName("java")] public JavaEmitterOptions? Java { get; set; }
        [JsonPropertyName("kotlin")] public JavaEmitterOptions? Kotlin { get; set; }
        [JsonPropertyName("jni")] public JniEmitterOptions? Jni { get; set; }


        [JsonPropertyName("ios")] public IosEmitterOptions? Ios { get; set; }
        [JsonPropertyName("tvos")] public TvosEmitterOptions? Tvos { get; set; }

        [JsonPropertyName("ios_swift")] public IosEmitterOptions? IosSwift { get; set; }
        [JsonPropertyName("tvos_swift")] public TvosEmitterOptions? TvosSwift { get; set; }

        [JsonPropertyName("ios_native")] public IosEmitterOptions? IosNative { get; set; }
        [JsonPropertyName("tvos_native")] public TvosEmitterOptions? TvosNative { get; set; }

        [JsonPropertyName("docs")] public DocEmitterOptions? Docs { get; set; }
    }
}
