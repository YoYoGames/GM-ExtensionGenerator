using extgen.Models.Config.Targets.Desktop;
using extgen.Models.Config.Targets.Mobile;
using System.Text.RegularExpressions;

namespace extgen.Emitters.Cargo
{
    public sealed class CargoEmitterSettings
    {
        public string CrateName { get; init; } = "extension";
        public bool HasWindows { get; init; }
        public bool HasMac { get; init; }
        public bool HasLinux { get; init; }
        public bool HasAndroid { get; init; }
        public bool HasIos { get; init; }
        public bool HasTvos { get; init; }
        public string ExtensionName { get; init; } = "MyExtension";

        public string WindowsOutput { get; init; } = "../";
        public string MacosOutput { get; init; } = "../";
        public string LinuxOutput { get; init; } = "../";
        public string AndroidOutput { get; init; } = "../AndroidSource";
        public string IosOutput { get; init; } = "../iOSSourceFromMac";
        public string TvosOutput { get; init; } = "../tvOSSourceFromMac";

        /// <summary>Dynamic framework basename for iOS/tvOS (e.g. <c>{Ext}_Rust</c>).</summary>
        public string IosFrameworkName { get; init; } = "MyExtension_Rust";

        public string IosBundleId { get; init; } = "com.extgen.extension.rust";
        public string MacosMinVersion { get; init; } = "11.0";
        public string IosMinVersion { get; init; } = "13.0";
        public string TvosMinVersion { get; init; } = "13.0";

        public static CargoEmitterSettings From(Planning.EmitterPlan plan)
        {
            var extName = plan.Config.Raw.GameMaker.Extension?.ExtensionName ?? plan.Runtime.ExtensionName;
            if (string.IsNullOrWhiteSpace(extName))
                extName = "MyExtension";

            var fwName = $"{extName}_Rust";
            var bundleSlug = Regex.Replace(extName.ToLowerInvariant(), @"[^a-z0-9]+", "");
            if (string.IsNullOrEmpty(bundleSlug))
                bundleSlug = "extension";

            return new()
            {
                CrateName = global::extgen.Emitters.Rust.RustEmitterSettings.SanitizeCrateName(extName),
                ExtensionName = extName,
                HasWindows = plan.Config.HasWindows,
                HasMac = plan.Config.HasMac,
                HasLinux = plan.Config.HasLinux,
                HasAndroid = plan.AndroidJni,
                HasIos = plan.IosEnabled && plan.AppleNative,
                HasTvos = plan.TvosEnabled && plan.AppleNative,
                WindowsOutput = plan.Config.Raw.Targets.Windows is WindowsTargetConfig { Enabled: true } w
                    ? w.Output
                    : "../",
                MacosOutput = plan.Config.Raw.Targets.MacOS is MacTargetConfig { Enabled: true } m
                    ? m.Output
                    : "../",
                LinuxOutput = plan.Config.Raw.Targets.Linux is LinuxTargetConfig { Enabled: true } l
                    ? l.Output
                    : "../",
                AndroidOutput = plan.Config.Raw.Targets.Android is AndroidTargetConfig { Enabled: true } a
                    ? a.Output
                    : "../AndroidSource",
                IosOutput = plan.Config.Raw.Targets.Ios is IosTargetConfig { Enabled: true } i
                    ? i.Output
                    : "../iOSSourceFromMac",
                TvosOutput = plan.Config.Raw.Targets.Tvos is TvosTargetConfig { Enabled: true } t
                    ? t.Output
                    : "../tvOSSourceFromMac",
                IosFrameworkName = fwName,
                IosBundleId = $"com.extgen.{bundleSlug}.rust",
            };
        }
    }
}
