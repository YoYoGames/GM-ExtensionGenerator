using extgen.Options;

namespace extgen.Emitters.Utils
{
    internal interface IEmitterContext<TargetOptions>
    {
        string ExtName { get; }
        TargetOptions Options { get; }
        RuntimeNaming Runtime { get; }
    }
}
