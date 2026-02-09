using extgen.Emitters.AppleMobile;
using extgen.Emitters.Cmake;
using extgen.Emitters.Doc;
using extgen.Emitters.Gml;
using extgen.Emitters.Yy;
using extgen.Models.Config.Build;
using extgen.Models.Config.Extras;
using extgen.Models.Config.Gml;
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
        // ----------------------------
        // Cmake
        // ----------------------------
        public static CmakeEmitterSettings ToSettings(this CmakeConfig cfg)
            => new()
            {
                CppExtensions = cfg.CppExtensions,
                CppStandard = cfg.CppStandard,
                EmitPresets = cfg.EmitPresets,
                StrictWarnings = cfg.StrictWarnings,
                UseThirdParty = cfg.UseThirdParty,
            };

        // ----------------------------
        // Android
        // ----------------------------
        public static AndroidEmitterSettings ToSettings(this AndroidTargetConfig cfg)
            => new()
            {
                OutputFolder = cfg.OutputFolder
            };

        // ----------------------------
        // Apple Mobile
        // ----------------------------
        public static IosEmitterSettings ToSettings(this IosTargetConfig cfg)
            => new()
            {
                OutputFolder = cfg.OutputFolder,
                SourceFolder = cfg.SourceFolder,
                SourceFilename = cfg.SourceFilename
            };

        public static TvosEmitterSettings ToSettings(this TvosTargetConfig cfg)
            => new()
            {
                OutputFolder = cfg.OutputFolder,
                SourceFolder = cfg.SourceFolder,
                SourceFilename = cfg.SourceFilename
            };

        // ----------------------------
        // Docs
        // ----------------------------
        public static DocEmitterSettings ToSettings(this DocsConfig cfg)
            => new()
            {
                OutputFolder = cfg.OutputFolder,
                OutputFilename = cfg.OutputFileName
            };

        // ----------------------------
        // GML + YY (driven by GmlConfig)
        // ----------------------------
        public static GmlEmitterSettings ToGmlSettings(this GmlConfig cfg)
            => new()
            {
                OutputFile = cfg.OutputFile,
                RuntimeFile = cfg.RuntimeFile,
                EmitRuntime = cfg.EmitRuntime
            };

        public static YyEmitterSettings ToYySettings(this GmlConfig cfg)
            => new()
            {
                OutputFile = cfg.DeclarationsFile
            };
    }
}
