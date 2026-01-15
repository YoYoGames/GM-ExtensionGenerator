using extgen.Emitters.Utils;
using extgen.Options;

namespace extgen.Emitters.Kotlin
{
    internal record KotlinEmitterContext(string ExtName, JavaEmitterOptions Options, RuntimeNaming Runtime) : IEmitterContext<JavaEmitterOptions>;
}
