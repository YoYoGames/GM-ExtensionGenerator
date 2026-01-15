using extgen.Emitters.Utils;
using extgen.Options;

namespace extgen.Emitters.Cpp
{
    internal sealed record CppEmitterContext(string ExtName, CppEmitterOptions Options, RuntimeNaming Runtime) : IEmitterContext<CppEmitterOptions>;
}
