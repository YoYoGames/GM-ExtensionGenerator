using extgen.Emitters.ObjcNative;
using extgen.Model;
using extgen.Options;
using extgen.Utils;

namespace extgen.Emitters.Cmake
{
    internal class CmakeEmitter(CodegenConfig options, Dictionary<string, IIrEmitter> emitters) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            var layout = new CmakeLayout(outputDir);
            var ctx = new CmakeEmitterContext(comp.Name, options);

            EmitAll(ctx, layout);
        }

        private void EmitAll(CmakeEmitterContext ctx, CmakeLayout layout) 
        {
            EmitMain(ctx, layout);

            EmitSource(layout);

            EmitScripts(layout);

            EmitThirdParty(layout);

            EmitCmakePresets(ctx, layout);

            EmitExtras(layout);
        }

        private static void EmitMain(CmakeEmitterContext ctx, CmakeLayout layout)
        {
            ResourceWriter.WriteTemplatedTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.CMakeLists.txt", Path.Combine(layout.RootDir, "CMakeLists.txt"), new Dictionary<string, string>
            {
                // Name
                ["EXTGEN_EXTENSION_NAME"] = ctx.ExtName,

                // Cpp
                ["EXTGEN_CPP_VERSION"] = ctx.Options.CppVersion,
                ["EXTGEN_CPP_EXTENSIONS"] = ctx.Options.CppExtensions ? "ON" : "OFF",

                // Config
                ["EXTGEN_USE_THIRD_PARTY"] = ctx.Options.UseThirdParty ? "ON" : "OFF",
                ["EXTGEN_STRICT_WARNINGS"] = ctx.Options.StrictWarnings ? "ON" : "OFF",
            });
        }

        private static void EmitSource(CmakeLayout layout) 
        {
            // This is for the third_party template only create IF there is no file yet
            if (!File.Exists(Path.Combine(layout.SourceDir, "CMakeLists.txt")))
            {
                ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.src.CMakeLists.txt", Path.Combine(layout.SourceDir, "CMakeLists.txt"));
            }
        }

        private void EmitScripts(CmakeLayout layout)
        {
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.cmake.extgen_xcframework.cmake", Path.Combine(layout.ScriptsDir, "extgen_xcframework.cmake"));
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.cmake.extgen_package_xcframework.cmake", Path.Combine(layout.ScriptsDir, "extgen_package_xcframework.cmake"));
        }

        private static void EmitThirdParty(CmakeLayout layout)
        {
            // This is for the third_party template only create IF there is no file yet
            if (!File.Exists(Path.Combine(layout.ThirdPartyDir, "CMakeLists.txt")))
            {
                ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.third_party.CMakeLists.txt", Path.Combine(layout.ThirdPartyDir, "CMakeLists.txt"));
            }
        }

        private void EmitCmakePresets(CmakeEmitterContext ctx, CmakeLayout layout)
        {
            var loweredTargets = ctx.Config.Targets.Select(t => t.ToLowerInvariant()).ToList();
            ResourceWriter.WriteTemplatedTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.CMakePresets.json", Path.Combine(layout.RootDir, "CMakePresets.json"), new Dictionary<string, string>
            {
                // Desktop
                ["EXTGEN_WINDOWS_DISABLED"] = loweredTargets.Contains("windows") ? "false" : "true",
                ["EXTGEN_MACOS_DISABLED"] = loweredTargets.Contains("macos") ? "false" : "true",
                ["EXTGEN_LINUX_DISABLED"] = loweredTargets.Contains("linux") ? "false" : "true",

                // Android
                ["EXTGEN_ANDROID_DISABLED"] = loweredTargets.Contains("android") ? "false" : "true",
                // iOS|tvOS (pure Objc|Swift)
                ["EXTGEN_IOS_DISABLED"] = emitters.TryGetValue("ios", out var ios_e) ? (ios_e is not ObjcNativeEmitter ? "false" : "true") : "true",
                ["EXTGEN_TVOS_DISABLED"] = emitters.TryGetValue("tvos", out var tvos_e) ? (tvos_e is not ObjcNativeEmitter ? "false" : "true") : "true",
                // iOS|tvOS (Objc wrapper)
                ["EXTGEN_IOS_NATIVE_DISABLED"] = emitters.TryGetValue("ios", out var ios_ne) ? (ios_ne is ObjcNativeEmitter ? "false" : "true") : "true",
                ["EXTGEN_TVOS_NATIVE_DISABLED"] = emitters.TryGetValue("tvos", out var tvos_ne) ? (tvos_ne is ObjcNativeEmitter ? "false" : "true") : "true",

                // Console
                ["EXTGEN_XBOX_DISABLED"] = loweredTargets.Contains("xbox") ? "false" : "true",
                ["EXTGEN_PS4_DISABLED"] = loweredTargets.Contains("ps4") ? "false" : "true",
                ["EXTGEN_PS5_DISABLED"] = loweredTargets.Contains("ps5") ? "false" : "true",
                ["EXTGEN_SWITCH_DISABLED"] = loweredTargets.Contains("switch") ? "false" : "true",
            });
        }

        private static void EmitExtras(CmakeLayout layout)
        {
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake..clang-format", Path.Combine(layout.RootDir, ".clang-format"));
        }
    }
}
