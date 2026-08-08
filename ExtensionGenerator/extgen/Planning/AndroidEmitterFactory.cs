using extgen.Emitters;
using extgen.Emitters.Android.Java;
using extgen.Emitters.Android.Jni;
using extgen.Emitters.Android.Kotlin;
using extgen.Mappers;
using extgen.Models.Config.Build;
using extgen.Models.Config.Targets.Mobile;

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
                AndroidMode.Jni => plan.Backend switch
                {
                    NativeBackend.Cpp => new JniEmitter(opts, runtime, new CppJniBridgeEmitter(opts, runtime), emitNativeCppDir: true),
                    NativeBackend.Rust => new JniEmitter(opts, runtime, new RustJniBridgeEmitter(runtime), emitNativeCppDir: false),
                    _ => throw new ArgumentOutOfRangeException(nameof(plan.Backend))
                },
                _ => throw new ArgumentOutOfRangeException(nameof(plan.Config.AndroidMode), plan.Config.AndroidMode, "Unknown AndroidMode")
            };
        }
    }
}
