using extgen.Emitters.AppleMobile;

namespace extgen.Planning
{
    /// <summary>
    /// High-level Apple native packaging contract for the current plan.
    /// Backend → contract mapping lives only in <see cref="AppleNativePackagingPolicy"/>.
    /// </summary>
    public enum AppleNativePackagingKind
    {
        /// <summary>Native + ObjC live in one extension XCFramework (C++/CMake model).</summary>
        BundledXcframework = 0,

        /// <summary>
        /// ObjC in GameMaker source folders; native symbols from a third-party (XC)Framework.
        /// </summary>
        GameMakerSourcesPlusExternalFramework = 1,
    }

    /// <summary>
    /// Resolved Apple packaging decisions consumed by Apple and YY emitters.
    /// </summary>
    public sealed class AppleNativePackaging
    {
        public AppleNativePackagingKind Kind { get; init; }

        public AppleMobileSourceLayout SourceLayout { get; init; }

        public AppleMobileNativeLink NativeLink { get; init; }

        /// <summary>
        /// Optional suffix after the extension name for YY third-party framework entries
        /// (e.g. <c>_Rust</c>). Null keeps YY defaults (extension name / embed 0).
        /// </summary>
        public string? ThirdPartyFrameworkSuffix { get; init; }

        /// <summary>GMExtensionFrameworkEntry.embed for the third-party framework.</summary>
        public int ThirdPartyFrameworkEmbed { get; init; }

        public static AppleNativePackaging BundledXcframework { get; } = new()
        {
            Kind = AppleNativePackagingKind.BundledXcframework,
            SourceLayout = AppleMobileSourceLayout.BundledInXcframework,
            NativeLink = AppleMobileNativeLink.BundledCppExports,
            ThirdPartyFrameworkSuffix = null,
            ThirdPartyFrameworkEmbed = 0,
        };
    }
}
