using extgen.Emitters.Java;
using extgen.Emitters.Utils;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem;

namespace extgen.Bridge.Java
{
    internal sealed class JavaBridge(
        IIrTypeMap types,
        RuntimeNaming runtime,
        JavaWireHelpers wireHelpers
    ) : JavaBridgeGenerator(types, runtime, wireHelpers)
    {
        protected override string GetTargetExpression(IEmitterContext<JavaEmitterOptions> ctx, IrFunction fn)
            => fn.Name;

        // Backing field / hooks remain default (no-op).
    }
}