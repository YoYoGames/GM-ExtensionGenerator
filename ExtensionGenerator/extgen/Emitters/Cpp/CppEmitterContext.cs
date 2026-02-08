using extgen.Emitters.Utils;
using extgen.Models.Config;

namespace extgen.Emitters.Cpp
{
    internal sealed record CppEmitterContext(string ExtName, CppEmitterSettings Options, RuntimeNaming Runtime) : IEmitterContext<CppEmitterSettings>;
}
