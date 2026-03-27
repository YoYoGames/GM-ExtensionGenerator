using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Config.Targets.Consoles;
using extgen.Models.Config.Targets.Desktop;
using extgen.Models.Config.Targets.Mobile;
using extgen.Utils;

namespace extgen.Emitters.Cmake
{
    internal class CmakeEmitter(CmakeEmitterSettings settings, ExtGenConfig config) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            var layout = new CmakeLayout(outputDir);
            var ctx = new CmakeEmitterContext(comp.Name, settings, config.Runtime);

            EmitAll(ctx, layout);
        }

        private void EmitAll(CmakeEmitterContext ctx, CmakeLayout layout) 
        {
            EmitMain(ctx, layout);

            EmitSource(layout);

            EmitScripts(ctx, layout);

            EmitThirdParty(layout);

            if (ctx.Settings.EmitPresets)
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
                ["EXTGEN_CPP_VERSION"] = $"{ctx.Settings.CppStandard}",
                ["EXTGEN_CPP_EXTENSIONS"] = ctx.Settings.CppExtensions ? "ON" : "OFF",

                // Config
                ["EXTGEN_USE_THIRD_PARTY"] = ctx.Settings.UseThirdParty ? "ON" : "OFF",
                ["EXTGEN_STRICT_WARNINGS"] = ctx.Settings.StrictWarnings ? "ON" : "OFF",
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

        private void EmitScripts(CmakeEmitterContext ctx, CmakeLayout layout)
        {
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.cmake.extgen_xcframework.cmake", Path.Combine(layout.ScriptsDir, "extgen_xcframework.cmake"));

            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.cmake.extgen_integrate_gamemaker_xcode.cmake", Path.Combine(layout.ScriptsDir, "extgen_integrate_gamemaker_xcode.cmake"));
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.cmake.extgen_integrate_gamemaker_xcode.rb", Path.Combine(layout.ScriptsDir, "extgen_integrate_gamemaker_xcode.rb"));
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.cmake.Gemfile", Path.Combine(layout.ScriptsDir, "Gemfile"));

            var targets = config.Targets;
            ResourceWriter.WriteTemplatedTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.cmake.extgen_package_xcframework.cmake", Path.Combine(layout.ScriptsDir, "extgen_package_xcframework.cmake"), new Dictionary<string, string>
            {
                // Frameworks
                ["EXTGEN_IOS_OUTPUT"] = targets.Ios?.Output ?? "../iOSSourceFromMac",
                ["EXTGEN_TVOS_OUTPUT"] = targets.Tvos?.Output ?? "../tvOSSourceFromMac",
            });
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
            var targets = config.Targets;
            ResourceWriter.WriteTemplatedTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.CMakePresets.json", Path.Combine(layout.RootDir, "CMakePresets.json"), new Dictionary<string, string>
            {
                // Desktop
                ["EXTGEN_WINDOWS_DISABLED"] = targets.Windows is WindowsTargetConfig { Enabled: true } ? "false" : "true",
                ["EXTGEN_WINDOWS_OUTPUT_FOLDER"] = targets.Windows is WindowsTargetConfig { Enabled: true } w ? w.Output : "../",

                ["EXTGEN_MACOS_DISABLED"] = targets.MacOS is MacTargetConfig { Enabled: true } ? "false" : "true",
                ["EXTGEN_MACOS_OUTPUT_FOLDER"] = targets.MacOS is MacTargetConfig { Enabled: true } m ? m.Output : "../",

                ["EXTGEN_LINUX_DISABLED"] = targets.Linux is LinuxTargetConfig { Enabled: true } ? "false" : "true",
                ["EXTGEN_LINUX_OUTPUT_FOLDER"] = targets.Linux is LinuxTargetConfig { Enabled: true } l ? l.Output : "../",

                // Android
                ["EXTGEN_ANDROID_DISABLED"] = targets.Android is AndroidTargetConfig { Enabled: true, Mode: AndroidMode.Jni } ? "false" : "true",
                ["EXTGEN_ANDROID_OUTPUT_FOLDER"] = targets.Android is AndroidTargetConfig { Enabled: true } a ? a.Output : "../AndroidSource",

                // iOS|tvOS (pure Objc|Swift)
                ["EXTGEN_IOS_DISABLED"] = targets.Ios is IosTargetConfig { Enabled: true, Mode: AppleMobileMode.Objc or AppleMobileMode.Swift } ? "false" : "true",
                ["EXTGEN_TVOS_DISABLED"] = targets.Tvos is TvosTargetConfig { Enabled: true, Mode: AppleMobileMode.Objc or AppleMobileMode.Swift } ? "false" : "true",

                // iOS|tvOS (Objc wrapper)
                ["EXTGEN_IOS_NATIVE_DISABLED"] = targets.Ios is IosTargetConfig { Enabled: true, Mode: AppleMobileMode.Native, Enabled: true } ? "false" : "true",
                ["EXTGEN_TVOS_NATIVE_DISABLED"] = targets.Tvos is TvosTargetConfig { Enabled: true, Mode: AppleMobileMode.Native } ? "false" : "true",

                ["EXTGEN_IOS_OUTPUT_FOLDER"] = targets.Ios is IosTargetConfig { Enabled: true } i ? i.Output : "../iOSSourceFromMac",
                ["EXTGEN_TVOS_OUTPUT_FOLDER"] = targets.Tvos is TvosTargetConfig { Enabled: true } t ? t.Output : "../tvOSSourceFromMac",

                // Console
                ["EXTGEN_XBOX_DISABLED"] = targets.Xbox is XboxTargetConfig { Enabled: true } ? "false" : "true",
                ["EXTGEN_XBOX_OUTPUT_FOLDER"] = targets.Xbox is XboxTargetConfig { Enabled: true } x ? x.Output : "../",

                ["EXTGEN_PS4_DISABLED"] = targets.Ps4 is Ps4TargetConfig { Enabled: true } ? "false" : "true",
                ["EXTGEN_PS4_OUTPUT_FOLDER"] = targets.Ps4 is Ps4TargetConfig { Enabled: true } ps4 ? ps4.Output : "../",

                ["EXTGEN_PS5_DISABLED"] = targets.Ps5 is Ps5TargetConfig { Enabled: true } ? "false" : "true",
                ["EXTGEN_PS5_OUTPUT_FOLDER"] = targets.Ps5 is Ps5TargetConfig { Enabled: true } ps5 ? ps5.Output : "../",

                ["EXTGEN_SWITCH_DISABLED"] = targets.Switch is SwitchTargetConfig { Enabled: true } ? "false" : "true",
                ["EXTGEN_SWITCH_OUTPUT_FOLDER"] = targets.Switch is SwitchTargetConfig { Enabled: true } s ? s.Output : "../",
                ["EXTGEN_SWITCH_USER_PROPS"] = targets.Switch is SwitchTargetConfig { Enabled: true, UserProps: not null } s1 ? PathUtils.ResolvePath(s1.UserProps, layout.RootDir) : "$env{EXT_SWITCH_USER_PROPS}",
            });
        }

        private static void EmitExtras(CmakeLayout layout)
        {
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake..clang-format", Path.Combine(layout.RootDir, ".clang-format"));
        }
    }
}
