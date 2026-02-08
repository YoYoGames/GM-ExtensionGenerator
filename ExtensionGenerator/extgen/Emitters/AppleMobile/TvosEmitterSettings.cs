
namespace extgen.Emitters.AppleMobile
{
    public sealed class TvosEmitterSettings : IAppleMobileEmitterSettings
    {
        public bool Enabled { get; set; } = true;
        public string SourceFolder { get; set; } = "./tvos";
        public string SourceFilename { get; set; } = "{0}_tvos";
        public string OutputFolder { get; set; } = "../tvOSSourceFromMac";

        public string Platform => "tvos";
    }
}
