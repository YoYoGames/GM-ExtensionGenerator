using extgen.Emitters.Android.Jni;

namespace extgen.Planning
{
    /// <summary>
    /// Resolved Android JNI packaging for the current native backend.
    /// </summary>
    public sealed class AndroidJniPackaging
    {
        public AndroidJniNativeBridgeKind BridgeKind { get; init; }

        /// <summary>
        /// When true, emit/clear the C++ JNI native code_gen directory under AndroidSource.
        /// </summary>
        public bool EmitNativeCppDir { get; init; }

        public static AndroidJniPackaging Cpp { get; } = new()
        {
            BridgeKind = AndroidJniNativeBridgeKind.Cpp,
            EmitNativeCppDir = true,
        };

        public static AndroidJniPackaging Rust { get; } = new()
        {
            BridgeKind = AndroidJniNativeBridgeKind.Rust,
            EmitNativeCppDir = false,
        };
    }
}
