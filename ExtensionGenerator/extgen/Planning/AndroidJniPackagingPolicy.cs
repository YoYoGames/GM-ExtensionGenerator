using extgen.Models.Config.Build;

namespace extgen.Planning
{
    /// <summary>
    /// Maps <see cref="NativeBackend"/> onto Android JNI packaging.
    /// Emitters/factories must branch on <see cref="AndroidJniPackaging"/>, not backend name.
    /// </summary>
    public static class AndroidJniPackagingPolicy
    {
        public static AndroidJniPackaging Resolve(NativeBackend backend) => backend switch
        {
            NativeBackend.Rust => AndroidJniPackaging.Rust,
            NativeBackend.Cpp => AndroidJniPackaging.Cpp,
            _ => AndroidJniPackaging.Cpp,
        };
    }
}
