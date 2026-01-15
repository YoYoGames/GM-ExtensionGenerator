using extgen.Bridge.Java;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem.Java;

namespace extgen.Emitters.Java
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
    internal sealed class JavaEmitter(JavaEmitterOptions options, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly JavaTypeMap typeMap = new();

        public void Emit(IrCompilation comp, string dir)
        {
            var ctx = new JavaEmitterContext(comp.Name, options, runtime);
            var layout = new JavaLayout(dir, options);

            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(JavaEmitterContext ctx, IrCompilation c, JavaLayout layout)
        {
            var wireHelpers = new JavaWireHelpers(ctx.Runtime, typeMap);
            var bridge = new JavaBridge(typeMap, ctx.Runtime, wireHelpers);

            var common = new JavaCommonEmitter(ctx, typeMap, bridge);
            common.EmitJavaArtifacts(c, layout);
            common.EmitJavaInterface(c, layout);
            common.EmitInternal(c, layout);
            common.EmitJavaUserShell(c, layout);
        }
    }
}
