using extgen.Models.Config;
using extgen.Models.Config.Build;
using extgen.Options.Android;
using extgen.Models.Config.Targets.Mobile;

namespace extgen.Planning
{
    /// <summary>
    /// Resolved emission decisions. Emitters receive settings derived from this plan
    /// and must not re-evaluate config conditionals.
    /// </summary>
    public sealed class EmitterPlan
    {
        public required ResolvedConfig Config { get; init; }

        public NativeBackend Backend { get; init; }
        public BuildSystemKind BuildSystem { get; init; }

        public bool NeedsNative { get; init; }
        public bool NeedsCpp { get; init; }
        public bool NeedsRust { get; init; }

        public bool AllowBindings { get; init; }
        public bool AllowBuild { get; init; }

        public bool AndroidEnabled { get; init; }
        public bool AndroidJni { get; init; }
        public bool IosEnabled { get; init; }
        public bool TvosEnabled { get; init; }
        public bool AppleNative { get; init; }

        public bool SkipInjectors { get; init; }

        public ExtGenConfig Raw => Config.Raw;
        public RuntimeNaming Runtime => Config.Raw.Runtime;
        public string OutputDir => Config.OutputDir;
    }
}
