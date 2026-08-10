namespace extgen.Emitters.AppleMobile
{
    public interface IAppleMobileEmitterSettings
    {
        public string Platform { get; }

        public string SourceFolder { get; set; }

        public string SourceFilename { get; set; }

        public string OutputSourceFolder { get; set; }

        /// <summary>Where ObjC/bridge sources are emitted. Set by <c>AppleNativePackagingPolicy</c>.</summary>
        public AppleMobileSourceLayout SourceLayout { get; set; }

        /// <summary>How ObjC native wrappers link to native exports. Set by <c>AppleNativePackagingPolicy</c>.</summary>
        public AppleMobileNativeLink NativeLink { get; set; }
    }
}
