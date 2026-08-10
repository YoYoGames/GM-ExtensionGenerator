
namespace extgen.Emitters.Yy
{
    public enum YyEmitterMode { Plain, Patch };

    public sealed class YyEmitterSettings
    {
        public required string OutputFile { get; set; }

        public YyEmitterMode Mode { get; set; }

        public required string? ExtensionName { get; set; }

        public required string? ExtensionFileName { get; set; }

        public bool PatchFrameworks { get; set; }

        public bool IosEnabled { get; set; }

        public bool TvosEnabled { get; set; }

        public bool AndroidEnabled { get; set; }

        /// <summary>
        /// Basename for ios/tvosThirdPartyFrameworkEntries (without .xcframework).
        /// Null/empty → compilation name.
        /// </summary>
        public string? ThirdPartyFrameworkBaseName { get; set; }

        /// <summary>GMExtensionFrameworkEntry.embed (0 = link, 1 = embed).</summary>
        public int ThirdPartyFrameworkEmbed { get; set; }
    }
}
