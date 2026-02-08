
namespace extgen.Emitters.Gml
{
    public sealed class GmlEmitterSettings
    {
        public bool EmitRuntime { get; set; }

        public required string OutputFile { get; set; }

        public required string RuntimeFile { get; set; }

    }
}
