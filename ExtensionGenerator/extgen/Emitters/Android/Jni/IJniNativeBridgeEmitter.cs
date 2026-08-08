using extgen.Models;
using extgen.Models.Config;

namespace extgen.Emitters.Android.Jni
{
    /// <summary>
    /// Emits the native half of JNI (C++ .cpp or Rust android_jni.rs).
    /// </summary>
    internal interface IJniNativeBridgeEmitter
    {
        void EmitBridge(
            JniEmitterContext ctx,
            IrCompilation comp,
            IReadOnlyList<JniFunctionSpec> specs,
            JniLayout layout);
    }
}
