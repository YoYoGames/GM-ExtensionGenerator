using extgen.Emitters.Android.Java;
using extgen.Emitters.Utils;
using extgen.Models;
using extgen.Models.Config;
using extgen.Options.Android;
using extgen.TypeSystem;

namespace extgen.Bridge.Java
{
    internal sealed class JavaBridge(
        IIrTypeMap types,
        RuntimeNaming runtime,
        JavaWireHelpers wireHelpers
    ) : JavaBridgeGenerator(types, runtime, wireHelpers)
    {
        protected override string GetTargetExpression(IEmitterContext<AndroidEmitterSettings> ctx, IrFunction fn)
            => fn.Name;

        // Backing field / hooks remain default (no-op).
    }
}