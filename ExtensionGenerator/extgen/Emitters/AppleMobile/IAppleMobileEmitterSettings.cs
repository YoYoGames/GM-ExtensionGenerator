
namespace extgen.Emitters.AppleMobile
{
    public interface IAppleMobileEmitterSettings
    {
        public string Platform { get; }

        public string SourceFolder { get; set; }

        public string SourceFilename { get; set; }
    }
}
