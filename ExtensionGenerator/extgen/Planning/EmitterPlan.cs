using extgen.Models.Config;
using extgen.Models.Config.Build;
using extgen.Options.Android;
using extgen.Models.Config.Targets.Mobile;

namespace extgen.Planning
{
    /// <summary>
    /// Resolved emission decisions. Emitters receive settings derived from this plan
    /// and must not re-evaluate config conditionals or branch on backend name for packaging.
    /// </summary>
    public sealed class EmitterPlan
    {
        public required ResolvedConfig Config { get; init; }

        public NativeBackend Backend { get; init; }
        public BuildSystemKind BuildSystem { get; init; }

        /// <summary>Portable FFI language emitter to run (Cpp / Rust / none).</summary>
        public PortableNativeLanguage PortableLanguage { get; init; }

        public bool NeedsNative => PortableLanguage != PortableNativeLanguage.None;
        public bool NeedsCpp => PortableLanguage == PortableNativeLanguage.Cpp;
        public bool NeedsRust => PortableLanguage == PortableNativeLanguage.Rust;

        public bool AllowBindings { get; init; }
        public bool AllowBuild { get; init; }

        public bool AndroidEnabled { get; init; }
        public bool AndroidJni { get; init; }
        public bool IosEnabled { get; init; }
        public bool TvosEnabled { get; init; }
        public bool AppleNative { get; init; }

        /// <summary>
        /// Backend-derived packaging and feature flags (Apple, Android JNI, injectors, …).
        /// </summary>
        public required NativeBackendFeatures BackendFeatures { get; init; }

        public AppleNativePackaging ApplePackaging => BackendFeatures.Apple;
        public AndroidJniPackaging AndroidJniPackaging => BackendFeatures.AndroidJni;
        public bool EmitCppInjectors => BackendFeatures.EmitCppInjectors;

        public ExtGenConfig Raw => Config.Raw;
        public RuntimeNaming Runtime => Config.Raw.Runtime;
        public string OutputDir => Config.OutputDir;
    }
}
