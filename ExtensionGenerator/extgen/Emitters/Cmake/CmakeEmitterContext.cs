using extgen.Emitters.Utils;
using extgen.Options;

namespace extgen.Emitters.Cmake
{
    internal sealed record CmakeEmitterContext(string ExtName, CodegenConfig Config) : IEmitterContext<CmakeEmitterOptions> 
    {
        public CmakeEmitterOptions Options => Config.Cmake;
        public RuntimeNaming Runtime => Config.Runtime;
    }
}
