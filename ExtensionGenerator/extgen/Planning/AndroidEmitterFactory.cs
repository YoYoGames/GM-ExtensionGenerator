using extgen.Emitters;
using extgen.Emitters.Android.Java;
using extgen.Emitters.Android.Jni;
using extgen.Emitters.Android.Kotlin;
using extgen.Mappers;
using extgen.Models.Config.Targets.Mobile;

namespace extgen.Planning
{
    public static class AndroidEmitterFactory
    {
        public static IIrEmitter Create(ResolvedConfig rc, AndroidTargetConfig cfg)
        {
            var opts = cfg.ToSettings();

            return rc.AndroidMode switch
            {
                AndroidMode.Kotlin => new KotlinEmitter(opts, rc.Raw.Runtime),
                AndroidMode.Java => new JavaEmitter(opts, rc.Raw.Runtime),
                AndroidMode.Jni => new JniEmitter(opts, rc.Raw.Runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(rc.AndroidMode), rc.AndroidMode, "Unknown AndroidMode")
            };
        }
    }
}
