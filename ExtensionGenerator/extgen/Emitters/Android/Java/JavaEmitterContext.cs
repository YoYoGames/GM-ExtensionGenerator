using extgen.Emitters.Utils;
using extgen.Models.Config;
using extgen.Options.Android;

namespace extgen.Emitters.Android.Java
{
    internal sealed record JavaEmitterContext(string ExtName, AndroidEmitterSettings Options, RuntimeNaming Runtime) : IEmitterContext<AndroidEmitterSettings>;
}
