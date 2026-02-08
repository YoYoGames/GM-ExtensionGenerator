using extgen.Emitters.AppleMobile;
using extgen.Emitters.Utils;
using extgen.Models.Config;

namespace extgen.Emitters.AppleMobile.Objc
{
    internal sealed record ObjcEmitterContext(string ExtName, IAppleMobileEmitterSettings Options, RuntimeNaming Runtime) : IEmitterContext<IAppleMobileEmitterSettings>;
}
