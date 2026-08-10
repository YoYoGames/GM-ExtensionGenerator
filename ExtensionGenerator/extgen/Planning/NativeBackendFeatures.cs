using extgen.Emitters.AppleMobile;
using extgen.Emitters.Yy;
using extgen.Models.Config.Build;

namespace extgen.Planning
{
    /// <summary>
    /// Cross-cutting capabilities derived from <see cref="NativeBackend"/>.
    /// Factories and emitters consume these fields instead of branching on backend name.
    /// </summary>
    public sealed class NativeBackendFeatures
    {
        public required AppleNativePackaging Apple { get; init; }

        public required AndroidJniPackaging AndroidJni { get; init; }

        /// <summary>
        /// C++ injectors (SharedLibrary_GetFunctionAddress / GMExtensionInitialise) only apply
        /// when the portable native layer is C++.
        /// </summary>
        public bool EmitCppInjectors { get; init; }
    }

    /// <summary>
    /// Single entry point that resolves all backend-derived packaging/feature flags and build system.
    /// Add new backends here (and in the nested packaging policies as needed).
    /// </summary>
    public static class NativeBackendPolicy
    {
        public static NativeBackendFeatures Resolve(NativeBackend backend, bool iosEnabled, bool tvosEnabled) =>
            new()
            {
                Apple = AppleNativePackagingPolicy.Resolve(backend, iosEnabled, tvosEnabled),
                AndroidJni = AndroidJniPackagingPolicy.Resolve(backend),
                EmitCppInjectors = backend switch
                {
                    NativeBackend.Cpp => true,
                    _ => false,
                },
            };

        public static BuildSystemKind ResolveBuildSystem(
            NativeBackend backend,
            bool allowBuild,
            bool needsNative,
            bool emitCmake)
        {
            if (!allowBuild || !needsNative)
                return BuildSystemKind.None;

            return backend switch
            {
                NativeBackend.Rust => BuildSystemKind.Cargo,
                NativeBackend.Cpp => emitCmake ? BuildSystemKind.Cmake : BuildSystemKind.None,
                _ => BuildSystemKind.None,
            };
        }
    }
}
