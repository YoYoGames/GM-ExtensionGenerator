using extgen.Emitters;
using extgen.Emitters.AppleMobile.Objc;
using extgen.Emitters.AppleMobile.ObjcNative;
using extgen.Emitters.AppleMobile.Swift;
using extgen.Models.Config.Targets.Mobile;
using extgen.Mappers;

namespace extgen.Planning
{
    public static class AppleEmitterFactory
    {
        public static IIrEmitter CreateIos(EmitterPlan plan, IosTargetConfig cfg)
        {
            var opts = cfg.ToSettings();
            var runtime = plan.Runtime;

            // ObjC native forwards to extern "C" — works for both Cpp and Rust staticlibs.
            return plan.Config.IosMode switch
            {
                AppleMobileMode.Objc => new ObjcEmitter(opts, runtime),
                AppleMobileMode.Swift => new SwiftEmitter(opts, runtime),
                AppleMobileMode.Native => new ObjcNativeEmitter(opts, runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(plan.Config.IosMode), plan.Config.IosMode, "Unknown AppleMobileMode")
            };
        }

        public static IIrEmitter CreateTvos(EmitterPlan plan, TvosTargetConfig cfg)
        {
            var opts = cfg.ToSettings();
            var runtime = plan.Runtime;

            return plan.Config.TvosMode switch
            {
                AppleMobileMode.Objc => new ObjcEmitter(opts, runtime),
                AppleMobileMode.Swift => new SwiftEmitter(opts, runtime),
                AppleMobileMode.Native => new ObjcNativeEmitter(opts, runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(plan.Config.TvosMode), plan.Config.TvosMode, "Unknown AppleMobileMode")
            };
        }
    }
}
