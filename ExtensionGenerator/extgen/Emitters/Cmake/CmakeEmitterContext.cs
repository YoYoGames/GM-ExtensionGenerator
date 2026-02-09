using extgen.Emitters.Utils;
using extgen.Models.Config;

namespace extgen.Emitters.Cmake
{
    internal sealed record CmakeEmitterContext(string ExtName, CmakeEmitterSettings Settings, RuntimeNaming Runtime) : IEmitterContext<CmakeEmitterSettings>;
}
