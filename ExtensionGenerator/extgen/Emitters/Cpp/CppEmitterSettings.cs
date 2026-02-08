using System.Text.Json.Serialization;

namespace extgen.Emitters.Cpp
{
    public sealed class CppEmitterSettings
    {
        public required string SourceFolder { get; set; }

        public required string SourceFilename { get; set; }
    }
}
