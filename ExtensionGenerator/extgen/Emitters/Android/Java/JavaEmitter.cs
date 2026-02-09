using extgen.Bridge.Java;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.Options.Android;
using extgen.TypeSystem.Java;

namespace extgen.Emitters.Android.Java
{
    /// <summary>
    /// Emits:
    ///   - {dir}/enums/{Enum}.java
    ///   - {dir}/records/{Struct}.java
    ///   - {dir}/codecs/{Struct}Codec.java
    ///   - {dir}/{ExtName}Internal.java    (auto-generated wrappers)
    ///   - {dir}/{ExtName}Interface.java   (required interface)
    ///   - {dir}/{ExtName}.java            (user-implemented functions; stubbed)
    /// </summary>
    internal sealed class JavaEmitter(AndroidEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly JavaTypeMap typeMap = new();

        public void Emit(IrCompilation comp, string dir)
        {
            var ctx = new JavaEmitterContext(comp.Name, settings, runtime);
            var layout = new JavaLayout(dir, settings);

            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(JavaEmitterContext ctx, IrCompilation c, JavaLayout layout)
        {
            var enums = new IrTypeEnumResolver(c.Enums);
            var wireHelpers = new JavaWireHelpers(ctx.Runtime, typeMap, enums);
            var bridge = new JavaBridge(typeMap, ctx.Runtime, wireHelpers);

            var common = new JavaCommonEmitter(ctx, typeMap, bridge);
            common.EmitJavaArtifacts(c, layout);
            common.EmitJavaInterface(c, layout);
            common.EmitInternal(c, layout);
            common.EmitJavaUserShell(c, layout);
        }
    }
}
