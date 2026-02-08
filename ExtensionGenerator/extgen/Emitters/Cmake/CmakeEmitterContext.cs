using extgen.Emitters.Utils;
using extgen.Models.Config;
using extgen.Models.Config.Build;

namespace extgen.Emitters.Cmake
{
    internal sealed record CmakeEmitterContext(string ExtName, ExtGenConfig Config) : IEmitterContext<CmakeEmitterOptions> 
    {
        public CmakeEmitterOptions Options => Config.Build.Cmake;
        public RuntimeNaming Runtime => Config.Runtime;
    }
}
