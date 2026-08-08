using extgen.Models.Config.Build;
using extgen.Models.Config.Targets.Mobile;
using extgen.Options.Android;

namespace extgen.Planning
{
    /// <summary>
    /// Builds a validated <see cref="EmitterPlan"/> from <see cref="ResolvedConfig"/>.
    /// </summary>
    public static class EmitterPlanBuilder
    {
        public static EmitterPlan Build(ResolvedConfig rc)
        {
            ArgumentNullException.ThrowIfNull(rc);

            var backend = rc.Raw.Build.NativeBackend;
            var wantsNativePlatforms = WantsNativePlatforms(rc);

            var needsCpp = backend == NativeBackend.Cpp && wantsNativePlatforms;
            var needsRust = backend == NativeBackend.Rust && WantsRustNativePlatforms(rc);
            var needsNative = needsCpp || needsRust;

            ValidateRustConstraints(rc, backend);

            var buildSystem = ResolveBuildSystem(rc, backend, needsNative);

            var androidJni = rc.AndroidEnabled && rc.AndroidMode == AndroidMode.Jni;
            var appleNative =
                (rc.IosEnabled && rc.IosMode == AppleMobileMode.Native) ||
                (rc.TvosEnabled && rc.TvosMode == AppleMobileMode.Native);

            return new EmitterPlan
            {
                Config = rc,
                Backend = backend,
                BuildSystem = buildSystem,
                NeedsNative = needsNative,
                NeedsCpp = needsCpp,
                NeedsRust = needsRust,
                AllowBindings = rc.AllowBindings,
                AllowBuild = rc.AllowBuild,
                AndroidEnabled = rc.AndroidEnabled,
                AndroidJni = androidJni,
                IosEnabled = rc.IosEnabled,
                TvosEnabled = rc.TvosEnabled,
                AppleNative = appleNative,
                SkipInjectors = backend == NativeBackend.Rust
            };
        }

        private static bool WantsNativePlatforms(ResolvedConfig rc) =>
            rc.HasWindows || rc.HasMac || rc.HasLinux ||
            rc.HasXbox || rc.HasPs4 || rc.HasPs5 || rc.HasSwitch ||
            (rc.AndroidEnabled && rc.AndroidMode == AndroidMode.Jni) ||
            (rc.IosEnabled && rc.IosMode == AppleMobileMode.Native) ||
            (rc.TvosEnabled && rc.TvosMode == AppleMobileMode.Native);

        private static bool WantsRustNativePlatforms(ResolvedConfig rc) =>
            rc.HasWindows || rc.HasMac || rc.HasLinux ||
            (rc.AndroidEnabled && rc.AndroidMode == AndroidMode.Jni) ||
            (rc.IosEnabled && rc.IosMode == AppleMobileMode.Native) ||
            (rc.TvosEnabled && rc.TvosMode == AppleMobileMode.Native);

        private static BuildSystemKind ResolveBuildSystem(ResolvedConfig rc, NativeBackend backend, bool needsNative)
        {
            if (!rc.AllowBuild || !needsNative)
                return BuildSystemKind.None;

            return backend switch
            {
                NativeBackend.Rust => BuildSystemKind.Cargo,
                NativeBackend.Cpp => rc.Raw.Build.EmitCmake ? BuildSystemKind.Cmake : BuildSystemKind.None,
                _ => BuildSystemKind.None
            };
        }

        private static void ValidateRustConstraints(ResolvedConfig rc, NativeBackend backend)
        {
            if (backend != NativeBackend.Rust)
                return;

            if (rc.HasXbox || rc.HasPs4 || rc.HasPs5 || rc.HasSwitch)
                throw new InvalidOperationException(
                    "nativeBackend=rust does not support console targets yet (Xbox/PS4/PS5/Switch).");

            if (rc.AndroidEnabled && rc.AndroidMode != AndroidMode.Jni)
                throw new InvalidOperationException(
                    "nativeBackend=rust requires targets.android.mode = \"jni\" when Android is enabled.");

            if (rc.IosEnabled && rc.IosMode != AppleMobileMode.Native)
                throw new InvalidOperationException(
                    "nativeBackend=rust requires targets.ios.mode = \"native\" when iOS is enabled.");

            if (rc.TvosEnabled && rc.TvosMode != AppleMobileMode.Native)
                throw new InvalidOperationException(
                    "nativeBackend=rust requires targets.tvos.mode = \"native\" when tvOS is enabled.");

            if (!WantsRustNativePlatforms(rc) &&
                (rc.HasWindows || rc.HasMac || rc.HasLinux || rc.AndroidEnabled || rc.IosEnabled || rc.TvosEnabled))
            {
                // Covered by mode checks above; keep for clarity if modes wrong.
            }

            if (!rc.HasWindows && !rc.HasMac && !rc.HasLinux &&
                !rc.AndroidEnabled && !rc.IosEnabled && !rc.TvosEnabled)
            {
                throw new InvalidOperationException(
                    "nativeBackend=rust requires at least one desktop or mobile (jni/native) target.");
            }
        }
    }
}
