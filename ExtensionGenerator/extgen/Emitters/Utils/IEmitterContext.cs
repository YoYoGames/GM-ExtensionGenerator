using extgen.Models.Config;

namespace extgen.Emitters.Utils
{
    internal interface IEmitterContext<TargetSettings>
    {
        string ExtName { get; }
        TargetSettings Settings { get; }
        RuntimeNaming Runtime { get; }
    }
}
