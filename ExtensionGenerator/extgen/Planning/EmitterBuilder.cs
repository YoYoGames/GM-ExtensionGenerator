using extgen.Emitters;
using extgen.Emitters.Cpp;
using extgen.Emitters.CppInjectors;
using extgen.Emitters.Doc;
using extgen.Emitters.Gml;
using extgen.Emitters.Rust;
using extgen.Emitters.Yy;
using extgen.Mappers;
using extgen.Models.Config.GameMaker;
using extgen.Models.Config.Targets.Mobile;

namespace extgen.Planning
{
    public static class EmitterBuilder
    {
        public static Dictionary<string, IIrEmitter> Build(EmitterPlan plan)
        {
            var emitters = new Dictionary<string, IIrEmitter>(StringComparer.OrdinalIgnoreCase);
            var rc = plan.Config;

            switch (plan.PortableLanguage)
            {
                case PortableNativeLanguage.Cpp:
                {
                    var cppSettings = new CppEmitterSettings
                    {
                        SourceFilename = rc.Raw.Targets.SourceFilename,
                        SourceFolder = rc.Raw.Targets.SourceFolder
                    };
                    emitters["cpp"] = new CppEmitter(cppSettings, plan.Runtime);
                    break;
                }
                case PortableNativeLanguage.Rust:
                    emitters["rust"] = new RustEmitter(RustEmitterSettings.From(plan), plan.Runtime);
                    break;
            }

            if (plan.AllowBindings)
            {
                if (rc.Raw.GameMaker.Wrappers is { Enabled: true } wrapperCfg)
                    emitters["gml"] = new GmlEmitter(wrapperCfg.ToSettings());

                if (rc.Raw.GameMaker.Runtime is { Enabled: true } runtimeCfg)
                    emitters["runtime"] = new GmlEmitter(runtimeCfg.ToSettings());

                if (rc.Raw.GameMaker.Extension is { Enabled: true } yyConfig)
                {
                    var yySettings = yyConfig.ToSettings(rc.AndroidEnabled, rc.IosEnabled, rc.TvosEnabled);
                    AppleNativePackagingPolicy.Apply(yySettings, plan);
                    emitters["extension"] = new YyEmitter(yySettings, plan.Runtime);
                }

                if (plan.EmitCppInjectors && rc.Raw.GameMaker.Injectors is { Enabled: true } injectorsCfg)
                {
                    ExtensionConfig extConfig = rc.Raw.GameMaker.Extension ?? new();
                    emitters["injectors"] = new CppInjectorsEmitter(injectorsCfg.ToSettings(extConfig), plan.Runtime);
                }
            }

            if (rc.Raw.Targets.Android is AndroidTargetConfig { Enabled: true } androidCfg)
                emitters["android"] = AndroidEmitterFactory.Create(plan, androidCfg);

            if (rc.Raw.Targets.Ios is IosTargetConfig { Enabled: true } iosCfg)
                emitters["ios"] = AppleEmitterFactory.CreateIos(plan, iosCfg);

            if (rc.Raw.Targets.Tvos is TvosTargetConfig { Enabled: true } tvosCfg)
                emitters["tvos"] = AppleEmitterFactory.CreateTvos(plan, tvosCfg);

            if (rc.Raw.Extras.Docs is { Enabled: true } d)
                emitters["docs"] = new DocEmitter(d.ToSettings(), plan.Runtime);

            return emitters;
        }

        /// <summary>Legacy overload — builds an <see cref="EmitterPlan"/> first.</summary>
        public static Dictionary<string, IIrEmitter> Build(ResolvedConfig rc) =>
            Build(EmitterPlanBuilder.Build(rc));
    }
}
