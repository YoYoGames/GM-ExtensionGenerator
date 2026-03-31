using extgen.Models.Config;

namespace extgen.Emitters.Utils
{
    /// <summary>
    /// Context passed to emitters containing target-specific settings and runtime naming.
    /// </summary>
    /// <typeparam name="TargetSettings">Target-specific settings type.</typeparam>
    internal interface IEmitterContext<TargetSettings>
    {
        /// <summary>Extension name.</summary>
        string ExtName { get; }

        /// <summary>Target-specific settings.</summary>
        TargetSettings Settings { get; }

        /// <summary>Runtime naming conventions.</summary>
        RuntimeNaming Runtime { get; }
    }
}
