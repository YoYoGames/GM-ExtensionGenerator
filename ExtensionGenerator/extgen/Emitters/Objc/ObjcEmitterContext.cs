using extgen.Emitters.Utils;
using extgen.Options;

namespace extgen.Emitters.Objc
{
    internal sealed record ObjcEmitterContext(string ExtName, IObjcEmitterOptions Options, RuntimeNaming Runtime) : IEmitterContext<IObjcEmitterOptions>;
}
