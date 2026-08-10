using extgen.Emitters.AppleMobile;
using extgen.Emitters.Yy;
using extgen.Models.Config.Build;

namespace extgen.Planning
{
    /// <summary>
    /// Single place that maps <see cref="NativeBackend"/> (and future backends) onto Apple
    /// source layout, native link mode, and YY third-party framework fields.
    /// Prefer resolving via <see cref="NativeBackendPolicy"/>; call this only from there
    /// or tests. Emitters must not branch on backend names for packaging.
    /// </summary>
    public static class AppleNativePackagingPolicy
    {
        public static AppleNativePackaging Resolve(NativeBackend backend, bool iosEnabled, bool tvosEnabled)
        {
            if (!iosEnabled && !tvosEnabled)
                return AppleNativePackaging.BundledXcframework;

            return backend switch
            {
                NativeBackend.Rust => new AppleNativePackaging
                {
                    Kind = AppleNativePackagingKind.GameMakerSourcesPlusExternalFramework,
                    SourceLayout = AppleMobileSourceLayout.GameMakerSourceTree,
                    NativeLink = AppleMobileNativeLink.ExternalCdylib,
                    ThirdPartyFrameworkSuffix = "_Rust",
                    ThirdPartyFrameworkEmbed = 1,
                },
                // C++ and any unknown backend: keep the historical CMake XCFramework contract.
                _ => AppleNativePackaging.BundledXcframework,
            };
        }

        public static void Apply(IAppleMobileEmitterSettings settings, AppleNativePackaging packaging)
        {
            settings.SourceLayout = packaging.SourceLayout;
            settings.NativeLink = packaging.NativeLink;
        }

        public static void Apply(YyEmitterSettings yy, EmitterPlan plan)
        {
            if (!plan.IosEnabled && !plan.TvosEnabled)
                return;

            var packaging = plan.ApplePackaging;
            if (packaging.Kind != AppleNativePackagingKind.GameMakerSourcesPlusExternalFramework)
                return;

            var baseName = yy.ExtensionName ?? plan.Runtime.ExtensionName;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "Extension";

            yy.ThirdPartyFrameworkBaseName = packaging.ThirdPartyFrameworkSuffix is null
                ? baseName
                : baseName + packaging.ThirdPartyFrameworkSuffix;
            yy.ThirdPartyFrameworkEmbed = packaging.ThirdPartyFrameworkEmbed;
        }
    }
}
