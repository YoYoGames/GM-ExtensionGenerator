using extgen.Bridge.Objc;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem.Objc;

namespace extgen.Emitters.Objc
{
    public sealed class ObjcEmitter(IObjcEmitterOptions options, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly ObjcTypeMap typeMap = new(runtime);

        public void Emit(IrCompilation comp, string dir)
        {
            ObjcEmitterContext ctx = new(comp.Name, options, runtime);
            ObjcLayout layout = new(dir, options);

            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(ObjcEmitterContext ctx, IrCompilation c, ObjcLayout layout)
        {
            ObjcBridge bridge = new();

            ObjcCommonEmitter common = new(ctx, typeMap, bridge);
            common.EmitInternal(c, layout);
            common.EmitObjcUserShell(c, layout);
        }
    }
}
