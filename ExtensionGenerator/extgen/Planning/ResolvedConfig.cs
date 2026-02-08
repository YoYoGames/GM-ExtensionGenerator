using extgen.Models.Config;
using extgen.Models.Config.Build;
using extgen.Models.Config.Targets.Mobile;
using extgen.Options.Android;

namespace extgen.Planning
{
    public sealed class ResolvedConfig
    {
        public ExtGenConfig Raw { get; }
        public string ConfigPath { get; }
        public string BaseDir { get; }
        public string InputPath { get; }
        public string OutputDir { get; }

        public BuildProfile Profile => Raw.Profile;

        public bool AllowBindings => Profile is BuildProfile.Full or BuildProfile.BindingsOnly;
        public bool AllowBuild => Profile is BuildProfile.Full or BuildProfile.BuildOnly;

        // Desktop / Consoles
        public bool HasWindows => Raw.Targets.Windows?.Enabled == true;
        public bool HasMac => Raw.Targets.MacOS?.Enabled == true;
        public bool HasLinux => Raw.Targets.Linux?.Enabled == true;

        public bool HasXbox => Raw.Targets.Xbox?.Enabled == true;
        public bool HasPs4 => Raw.Targets.Ps4?.Enabled == true;
        public bool HasPs5 => Raw.Targets.Ps5?.Enabled == true;
        public bool HasSwitch => Raw.Targets.Switch?.Enabled == true;

        // Mobile
        public bool AndroidEnabled => Raw.Targets.Android?.Enabled == true;
        public bool IosEnabled => Raw.Targets.Ios?.Enabled == true;
        public bool TvosEnabled => Raw.Targets.Tvos?.Enabled == true;

        public AndroidMode AndroidMode => Raw.Targets.Android?.Mode ?? AndroidMode.Java;

        public AppleMobileMode IosMode => Raw.Targets.Ios?.Mode ?? AppleMobileMode.Objc;
        public AppleMobileMode TvosMode => Raw.Targets.Tvos?.Mode ?? AppleMobileMode.Objc;

        public bool NeedsCpp =>
            HasWindows || HasMac || HasLinux ||
            HasXbox || HasPs4 || HasPs5 || HasSwitch ||
            (AndroidEnabled && AndroidMode == AndroidMode.Jni) ||
            (IosEnabled && IosMode == AppleMobileMode.Native) ||
            (TvosEnabled && TvosMode == AppleMobileMode.Native);

        public ResolvedConfig(ExtGenConfig raw, string configPath, string baseDir, string inputPath, string outputDir)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            ConfigPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            BaseDir = baseDir ?? throw new ArgumentNullException(nameof(baseDir));
            InputPath = inputPath ?? throw new ArgumentNullException(nameof(inputPath));
            OutputDir = outputDir ?? throw new ArgumentNullException(nameof(outputDir));
        }

        public void Validate()
        {
            // If enabled, block must exist (these are sanity checks)
            if (AndroidEnabled && Raw.Targets.Android is null)
                throw new InvalidOperationException("Android enabled but Targets.Android is null.");
            if (IosEnabled && Raw.Targets.Ios is null)
                throw new InvalidOperationException("iOS enabled but Targets.Ios is null.");
            if (TvosEnabled && Raw.Targets.Tvos is null)
                throw new InvalidOperationException("tvOS enabled but Targets.Tvos is null.");
        }
    }
}
