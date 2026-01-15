using extgen.Emitters.Utils;
using extgen.Options;

namespace extgen.Emitters.Yy;

internal sealed record YyEmitterContext(string ExtName, YyEmitterOptions Options, RuntimeNaming Runtime) : IEmitterContext<YyEmitterOptions>;
