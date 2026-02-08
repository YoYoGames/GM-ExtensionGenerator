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
        public static IIrEmitter CreateIos(ResolvedConfig rc, IosTargetConfig cfg)
        {
            var opts = cfg.ToSettings();

            return rc.IosMode switch
            {
                AppleMobileMode.Objc => new ObjcEmitter(opts, rc.Raw.Runtime),
                AppleMobileMode.Swift => new SwiftEmitter(opts, rc.Raw.Runtime),
                AppleMobileMode.Native => new ObjcNativeEmitter(opts, rc.Raw.Runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(rc.IosMode), rc.IosMode, "Unknown AppleMobileMode")
            };
        }

        public static IIrEmitter CreateTvos(ResolvedConfig rc, TvosTargetConfig cfg)
        {
            var opts = cfg.ToSettings();

            return rc.TvosMode switch
            {
                AppleMobileMode.Objc => new ObjcEmitter(opts, rc.Raw.Runtime),
                AppleMobileMode.Swift => new SwiftEmitter(opts, rc.Raw.Runtime),
                AppleMobileMode.Native => new ObjcNativeEmitter(opts, rc.Raw.Runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(rc.TvosMode), rc.TvosMode, "Unknown AppleMobileMode")
            };
        }
    }
}
