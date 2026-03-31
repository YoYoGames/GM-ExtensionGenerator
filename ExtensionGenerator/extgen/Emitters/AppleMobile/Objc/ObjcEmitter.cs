using extgen.Bridge.Objc;
using extgen.Models.Utils;
using extgen.Models;
using extgen.Models.Config;
using extgen.TypeSystem.Objc;

namespace extgen.Emitters.AppleMobile.Objc
{
    /// <summary>
    /// Emits Objective-C code for iOS/tvOS extensions.
    /// </summary>
    public sealed class ObjcEmitter(IAppleMobileEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly ObjcTypeMap typeMap = new(runtime);

        /// <summary>
        /// Emits the Objective-C implementation for the given compilation.
        /// </summary>
        public void Emit(IrCompilation comp, string dir)
        {
            ObjcEmitterContext ctx = new(comp.Name, settings, runtime);
            ObjcLayout layout = new(dir, settings);

            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(ObjcEmitterContext ctx, IrCompilation c, ObjcLayout layout)
        {
            var enums = new IrTypeEnumResolver(c.Enums);
            ObjcBridge bridge = new(enums);

            ObjcCommonEmitter common = new(ctx, typeMap, bridge);
            common.EmitInternal(c, layout);
            common.EmitObjcUserShell(c, layout);
        }
    }
}
