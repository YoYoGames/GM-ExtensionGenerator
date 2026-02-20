using extgen.Emitters;
using extgen.Emitters.Cpp;
using extgen.Emitters.Doc;
using extgen.Emitters.Gml;
using extgen.Emitters.Yy;
using extgen.Mappers;
using extgen.Models.Config.Targets.Mobile;

namespace extgen.Planning
{
    public static class EmitterBuilder
    {
        public static Dictionary<string, IIrEmitter> Build(ResolvedConfig rc)
        {
            var emitters = new Dictionary<string, IIrEmitter>(StringComparer.OrdinalIgnoreCase);

            // Core C++ only if required
            if (rc.NeedsCpp)
            {
                var cppSettings = new CppEmitterSettings
                {
                    SourceFilename = rc.Raw.Targets.SourceFilename,
                    SourceFolder = rc.Raw.Targets.SourceFolder
                };
                emitters["cpp"] = new CppEmitter(cppSettings, rc.Raw.Runtime);
            }

            // Bindings (GML + YY are coupled)
            if (rc.AllowBindings && rc.Raw.Gml is { Enabled: true } g)
            {
                emitters["gml"] = new GmlEmitter(g.ToGmlSettings());
                emitters["yy"] = new YyEmitter(g.ToYySettings(rc.AndroidEnabled, rc.IosEnabled, rc.TvosEnabled), rc.Raw.Runtime);
            }

            // Android
            if (rc.Raw.Targets.Android is AndroidTargetConfig { Enabled: true } androidCfg)
            {
                emitters["android"] = AndroidEmitterFactory.Create(rc, androidCfg);
            }

            // iOS
            if (rc.Raw.Targets.Ios is IosTargetConfig { Enabled: true } iosCfg)
            {
                emitters["ios"] = AppleEmitterFactory.CreateIos(rc, iosCfg);
            }

            // tvOS
            if (rc.Raw.Targets.Tvos is TvosTargetConfig { Enabled: true } tvosCfg)
            {
                emitters["tvos"] = AppleEmitterFactory.CreateTvos(rc, tvosCfg);
            }

            // Docs (extras)
            if (rc.Raw.Extras.Docs is { Enabled: true } d)
            {
                emitters["docs"] = new DocEmitter(d.ToSettings(), rc.Raw.Runtime);
            }

            return emitters;
        }
    }
}
