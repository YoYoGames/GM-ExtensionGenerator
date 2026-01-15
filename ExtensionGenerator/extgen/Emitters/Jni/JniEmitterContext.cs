using extgen.Emitters.Utils;
using extgen.Options;

namespace extgen.Emitters.Jni
{
    internal sealed record JniEmitterContext(string ExtName, JniEmitterOptions Options, RuntimeNaming Runtime) : IEmitterContext<JniEmitterOptions>
    {
        public string BridgeClass => $"{ExtName}Bridge";
        public string BridgePackageUnderscore => Runtime.BridgePackage.Replace('.', '_');
        public string LibraryName => string.Format(Runtime.LibraryNameFormat, ExtName);
    }
}
