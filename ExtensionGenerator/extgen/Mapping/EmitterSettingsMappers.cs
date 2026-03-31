using extgen.Emitters.AppleMobile;
using extgen.Emitters.Cmake;
using extgen.Emitters.CppInjectors;
using extgen.Emitters.Doc;
using extgen.Emitters.Gml;
using extgen.Emitters.Yy;
using extgen.Models.Config.Build;
using extgen.Models.Config.Extras;
using extgen.Models.Config.GameMaker;
using extgen.Models.Config.Targets.Mobile;
using extgen.Options.Android;

namespace extgen.Mappers
{
    /// <summary>
    /// Single source of truth for Config -> EmitterSettings mapping.
    /// Keeps Settings clean and prevents "interface-enforced" weirdness.
    /// </summary>
    public static class EmitterSettingsMappers
    {
        public static CmakeEmitterSettings ToSettings(this CmakeConfig cfg)
            => new()
            {
                CppExtensions = cfg.CppExtensions,
                CppStandard = cfg.CppStandard,
                EmitPresets = cfg.EmitPresets,
                StrictWarnings = cfg.StrictWarnings,
                UseThirdParty = cfg.UseThirdParty,
            };

        public static AndroidEmitterSettings ToSettings(this AndroidTargetConfig cfg)
            => new()
            {
                OutputFolder = cfg.Output
            };

        public static IosEmitterSettings ToSettings(this IosTargetConfig cfg)
            => new()
            {
                OutputFolder = cfg.Output,
                SourceFolder = cfg.SourceFolder,
                SourceFilename = cfg.SourceFilename,
                OutputSourceFolder = cfg.OutputSource
            };

        public static TvosEmitterSettings ToSettings(this TvosTargetConfig cfg)
            => new()
            {
                OutputFolder = cfg.Output,
                SourceFolder = cfg.SourceFolder,
                SourceFilename = cfg.SourceFilename,
                OutputSourceFolder = cfg.OutputSource
            };

        public static DocEmitterSettings ToSettings(this DocsConfig cfg)
            => new()
            {
                OutputFile = cfg.Output,
                Overwrite = cfg.Overwrite
            };

        public static GmlEmitterSettings ToSettings(this WrapperConfig cfg)
            => new()
            {
                OutputFile = cfg.Output,
                Mode = GmlEmitterMode.Wrapper
            };

        public static GmlEmitterSettings ToSettings(this RuntimeConfig cfg)
            => new()
            {
                OutputFile = cfg.OutputFile,
                Mode = GmlEmitterMode.Runtime
            };

        public static YyEmitterSettings ToSettings(this ExtensionConfig cfg, bool androidEnabled, bool iosEnabled, bool tvosEnabled)
            => new()
            {
                OutputFile = cfg.OutputFile,
                Mode = cfg.Mode == YyMode.Patch ? YyEmitterMode.Patch : YyEmitterMode.Plain,
                ExtensionName = cfg.ExtensionName,
                ExtensionFileName = cfg.ExtensionFileName,
                PatchFrameworks = cfg.PatchFrameworks,
                AndroidEnabled = androidEnabled,
                IosEnabled = iosEnabled,
                TvosEnabled = tvosEnabled
            };

        public static CppInjectorsEmitterSettings ToSettings(this InjectorsConfig cfg, ExtensionConfig yyConfig)
        => new()
        {
            OutputFolder = cfg.OutputFolder,
            ExtensionName = yyConfig.ExtensionName,
            ExtensionFileName = yyConfig.ExtensionFileName
        };
    }
}
