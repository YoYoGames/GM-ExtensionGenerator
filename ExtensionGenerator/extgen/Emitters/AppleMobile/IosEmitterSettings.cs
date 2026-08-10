namespace extgen.Emitters.AppleMobile
{
    public sealed class IosEmitterSettings : IAppleMobileEmitterSettings
    {
        public string SourceFolder { get; set; } = "./ios";
        public string SourceFilename { get; set; } = "{0}_ios";
        public string OutputFolder { get; set; } = "../iOSSourceFromMac";
        public string OutputSourceFolder { get; set; } = "../iOSSource";
        public AppleMobileSourceLayout SourceLayout { get; set; }
        public AppleMobileNativeLink NativeLink { get; set; }

        public string Platform => "ios";
    }
}
