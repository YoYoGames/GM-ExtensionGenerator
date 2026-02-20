using extgen.Bridge.Objc;
using extgen.Emitters.AppleMobile.Objc;
using extgen.Models.Utils;

namespace extgen.Bridge.ObjcNative
{
    internal class ObjcNativeBridge(IIrTypeEnumResolver enums) : ObjcBridge(enums)
    {
        // There are no protocols being emitted for native based extensions
        public override IEnumerable<string>? UserShellProtocols(ObjcEmitterContext ctx) => null;
    }
}
