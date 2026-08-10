namespace extgen.Emitters.Android.Jni
{
    /// <summary>
    /// Which native half of the JNI bridge to emit.
    /// </summary>
    public enum AndroidJniNativeBridgeKind
    {
        /// <summary>C++ <c>*Internal_jni.cpp</c> under Android native code_gen.</summary>
        Cpp = 0,

        /// <summary>Rust <c>android_jni.rs</c> under the Cargo crate.</summary>
        Rust = 1,
    }
}
