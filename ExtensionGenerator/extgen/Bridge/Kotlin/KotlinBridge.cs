using codegencore.Writers.Lang;
using extgen.Bridge.Java;
using extgen.Emitters.Java;
using extgen.Emitters.Utils;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem;

namespace extgen.Bridge.Kotlin
{
    internal sealed class KotlinBridge(
        IIrTypeMap types,
        RuntimeNaming runtime,
        JavaWireHelpers wireHelpers
    ) : JavaBridgeGenerator(types, runtime, wireHelpers)
    {
        public override void EmitBackingField(IEmitterContext<JavaEmitterOptions> ctx, JavaWriter w)
        {
            var ext = ctx.ExtName;
            w.Field(
                type: $"{ext}Kotlin",
                name: "__kotlin_instance",
                initializer: $"new {ext}Kotlin()",
                modifiers: ["private", "final"]
            ).Line();
        }

        protected override string GetTargetExpression(IEmitterContext<JavaEmitterOptions> ctx, IrFunction fn)
            => $"__kotlin_instance.{fn.Name}";

        public override string[]? GetClassImplements(IEmitterContext<JavaEmitterOptions> ctx)
            => null;
    }
}
