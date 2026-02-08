using codegencore.Writers.Lang;
using extgen.Bridge.Java;
using extgen.Emitters.Android.Java;
using extgen.Emitters.Utils;
using extgen.Models;
using extgen.Models.Config;
using extgen.Options.Android;
using extgen.TypeSystem;

namespace extgen.Bridge.Kotlin
{
    internal sealed class KotlinBridge(
        IIrTypeMap types,
        RuntimeNaming runtime,
        JavaWireHelpers wireHelpers
    ) : JavaBridgeGenerator(types, runtime, wireHelpers)
    {
        public override void EmitBackingField(IEmitterContext<AndroidEmitterSettings> ctx, JavaWriter w)
        {
            var ext = ctx.ExtName;
            w.Field(
                type: $"{ext}Kotlin",
                name: "__kotlin_instance",
                initializer: $"new {ext}Kotlin()",
                modifiers: ["private", "final"]
            ).Line();
        }

        protected override string GetTargetExpression(IEmitterContext<AndroidEmitterSettings> ctx, IrFunction fn)
            => $"__kotlin_instance.{fn.Name}";

        public override string[]? GetClassImplements(IEmitterContext<AndroidEmitterSettings> ctx)
            => null;
    }
}
