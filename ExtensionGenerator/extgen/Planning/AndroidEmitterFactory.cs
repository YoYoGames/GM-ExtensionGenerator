using extgen.Emitters;
using extgen.Emitters.Android.Java;
using extgen.Emitters.Android.Jni;
using extgen.Emitters.Android.Kotlin;
using extgen.Mappers;
using extgen.Models.Config;
using extgen.Models.Config.Targets.Mobile;
using extgen.Options.Android;

namespace extgen.Planning
{
    public static class AndroidEmitterFactory
    {
        public static IIrEmitter Create(EmitterPlan plan, AndroidTargetConfig cfg)
        {
            var opts = cfg.ToSettings();
            var runtime = plan.Runtime;

            return plan.Config.AndroidMode switch
            {
                AndroidMode.Kotlin => new KotlinEmitter(opts, runtime),
                AndroidMode.Java => new JavaEmitter(opts, runtime),
                AndroidMode.Jni => CreateJni(plan, opts, runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(plan.Config.AndroidMode), plan.Config.AndroidMode, "Unknown AndroidMode")
            };
        }

        private static IIrEmitter CreateJni(EmitterPlan plan, AndroidEmitterSettings opts, RuntimeNaming runtime)
        {
            var packaging = plan.AndroidJniPackaging;
            IJniNativeBridgeEmitter bridge = packaging.BridgeKind switch
            {
                AndroidJniNativeBridgeKind.Cpp => new CppJniBridgeEmitter(opts, runtime),
                AndroidJniNativeBridgeKind.Rust => new RustJniBridgeEmitter(runtime),
                _ => throw new ArgumentOutOfRangeException(nameof(packaging.BridgeKind), packaging.BridgeKind, "Unknown AndroidJniNativeBridgeKind")
            };

            return new JniEmitter(opts, runtime, bridge, packaging.EmitNativeCppDir);
        }
    }
}
