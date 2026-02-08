using extgen.Emitters.Utils;
using extgen.Models.Config;
using extgen.Options.Android;

namespace extgen.Emitters.Android.Kotlin
{
    internal record KotlinEmitterContext(string ExtName, AndroidEmitterSettings Options, RuntimeNaming Runtime) : IEmitterContext<AndroidEmitterSettings>;
}
