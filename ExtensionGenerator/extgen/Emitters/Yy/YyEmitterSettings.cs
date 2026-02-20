
namespace extgen.Emitters.Yy
{
    public sealed class YyEmitterSettings
    {
        public required string OutputFile { get; set; }

        public bool PatchFrameworks { get; set; }

        public bool IosEnabled { get; set; }

        public bool TvosEnabled { get; set; }

        public bool AndroidEnabled { get; set; }
    }
}
