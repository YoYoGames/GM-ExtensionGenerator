using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Config.Targets.Consoles;
using extgen.Models.Config.Targets.Desktop;
using extgen.Models.Config.Targets.Mobile;
using extgen.Utils;

namespace extgen.Emitters.Cmake
{
    /// <summary>
    /// Emits CMake build configuration files for cross-platform extension compilation.
    /// </summary>
    internal class CmakeEmitter(CmakeEmitterSettings settings, ExtGenConfig config) : IIrEmitter
    {
        /// <summary>
        /// Emits the CMake configuration for the given compilation.
        /// </summary>
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

            EmitTemplates(ctx, layout);

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
            // Only create if the file does not exist yet
            if (!File.Exists(Path.Combine(layout.SourceDir, "CMakeLists.txt")))
            {
                ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.src.CMakeLists.txt", Path.Combine(layout.SourceDir, "CMakeLists.txt"));
            }
        }

        private void EmitScripts(CmakeEmitterContext ctx, CmakeLayout layout)
        {
            var targets = config.Targets;

            // Desktop
            EmitPlatformScripts(layout.ScriptsDir, "android",  postProject: true, postBuild: true);
            EmitPlatformScripts(layout.ScriptsDir, "windows",  postProject: true, postBuild: true);
            EmitPlatformScripts(layout.ScriptsDir, "macos",    postProject: true, postBuild: true);
            EmitPlatformScripts(layout.ScriptsDir, "linux",    postProject: true, postBuild: true);

            // Apple mobile
            EmitPlatformScripts(layout.ScriptsDir, "ios",  postProject: true, postBuild: false);
            EmitPlatformScripts(layout.ScriptsDir, "tvos", postProject: true, postBuild: false);

            var appleMobileDir = Path.Combine(layout.ScriptsDir, "apple_mobile");
            EmitScriptResource("apple_mobile.extgen_post_build.cmake",         appleMobileDir, "extgen_post_build.cmake");
            EmitScriptResource("apple_mobile.extgen_xcframework_targets.cmake", appleMobileDir, "extgen_xcframework_targets.cmake");
            EmitScriptResource("apple_mobile.extgen_xcframework_package.cmake", appleMobileDir, "extgen_xcframework_package.cmake");
            EmitScriptResource("apple_mobile.extgen_xcode_integrate.cmake",     appleMobileDir, "extgen_xcode_integrate.cmake");
            EmitScriptResource("apple_mobile.extgen_xcode_integrate.rb",        appleMobileDir, "extgen_xcode_integrate.rb");
            EmitScriptResource("apple_mobile.Gemfile",                          appleMobileDir, "Gemfile");

            // Switch
            if (targets.Switch is SwitchTargetConfig { Enabled: true })
            {
                var switchDir = Path.Combine(layout.ScriptsDir, "switch");
                EmitPlatformScripts(layout.ScriptsDir, "switch", postProject: true, postBuild: true);
                EmitScriptResource("switch.extgen_pre_project.cmake", switchDir, "extgen_pre_project.cmake");
                EmitScriptResource("switch.Directory.Build.props.in", switchDir, "Directory.Build.props.in");
            }

            // PS4
            if (targets.Ps4 is Ps4TargetConfig { Enabled: true })
            {
                EmitPlatformScripts(layout.ScriptsDir, "ps4", postProject: true, postBuild: true);
                EmitScriptResource("ps4.extgen_pre_project.cmake", Path.Combine(layout.ScriptsDir, "ps4"), "extgen_pre_project.cmake");
            }

            // PS5
            if (targets.Ps5 is Ps5TargetConfig { Enabled: true })
            {
                EmitPlatformScripts(layout.ScriptsDir, "ps5", postProject: true, postBuild: true);
                EmitScriptResource("ps5.extgen_pre_project.cmake", Path.Combine(layout.ScriptsDir, "ps5"), "extgen_pre_project.cmake");
            }

            // Xbox - one config entry covers both Xbox One and Scarlett
            if (targets.Xbox is XboxTargetConfig { Enabled: true })
            {
                EmitPlatformScripts(layout.ScriptsDir, "xbox_one",      postProject: true, postBuild: true);
                EmitScriptResource("xbox_one.extgen_pre_project.cmake",      Path.Combine(layout.ScriptsDir, "xbox_one"),      "extgen_pre_project.cmake");

                EmitPlatformScripts(layout.ScriptsDir, "xbox_scarlett", postProject: true, postBuild: true);
                EmitScriptResource("xbox_scarlett.extgen_pre_project.cmake", Path.Combine(layout.ScriptsDir, "xbox_scarlett"), "extgen_pre_project.cmake");
            }
        }

        private void EmitPlatformScripts(string scriptsDir, string platform, bool postProject, bool postBuild)
        {
            var dir = Path.Combine(scriptsDir, platform);
            if (postProject) EmitScriptResource($"{platform}.extgen_post_project.cmake", dir, "extgen_post_project.cmake");
            if (postBuild) EmitScriptResource($"{platform}.extgen_post_build.cmake", dir, "extgen_post_build.cmake");
        }

        private static void EmitScriptResource(string resourceSuffix, string destDir, string fileName)
        {
            ResourceWriter.WriteTextResource(
                typeof(Program).Assembly,
                $"extgen.Resources.Cmake.cmake.{resourceSuffix}",
                Path.Combine(destDir, fileName));
        }

        private static void EmitTemplates(CmakeEmitterContext ctx, CmakeLayout layout)
        {
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake.templates.CMakeUserPresets.json.template", Path.Combine(layout.TemplateDir, "CMakeUserPresets.json.template"));
        }

        private static void EmitThirdParty(CmakeLayout layout)
        {
            // Only create if the file does not exist yet
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
            });
        }

        private static void EmitExtras(CmakeLayout layout)
        {
            if (!File.Exists(Path.Combine(layout.RootDir, ".clang-format")))
            {
                ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake..clang-format", Path.Combine(layout.RootDir, ".clang-format"));
            }

            if (!File.Exists(Path.Combine(layout.RootDir, ".gitignore")))
            {
                ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cmake..gitignore", Path.Combine(layout.RootDir, ".gitignore"));
            }
        }
    }
}
