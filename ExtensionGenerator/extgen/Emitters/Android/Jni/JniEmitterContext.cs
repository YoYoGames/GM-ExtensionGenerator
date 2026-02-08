using extgen.Emitters.Utils;
using extgen.Models.Config;
using extgen.Options.Android;

namespace extgen.Emitters.Android.Jni
{
    internal sealed record JniEmitterContext(string ExtName, AndroidEmitterSettings Options, RuntimeNaming Runtime) : IEmitterContext<AndroidEmitterSettings>
    {
        public string BridgeClass => $"{ExtName}Bridge";
        public string BridgePackageUnderscore => Runtime.BridgePackage.Replace('.', '_');
        public string LibraryName => string.Format(Runtime.LibraryNameFormat, ExtName);
    }
}
