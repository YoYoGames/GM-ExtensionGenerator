using extgen.Emitters.Utils;
using extgen.Models.Config;

namespace extgen.Emitters.Yy;

internal sealed record YyEmitterContext(string ExtName, YyEmitterSettings Options, RuntimeNaming Runtime) : IEmitterContext<YyEmitterSettings>;
