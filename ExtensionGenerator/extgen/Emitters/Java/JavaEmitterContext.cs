using extgen.Emitters.Utils;
using extgen.Options;

namespace extgen.Emitters.Java
{
    internal sealed record JavaEmitterContext(string ExtName, JavaEmitterOptions Options, RuntimeNaming Runtime) : IEmitterContext<JavaEmitterOptions>;
}
