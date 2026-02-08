using codegencore.Writers.Lang;
using extgen.Emitters.AppleMobile.Objc;
using extgen.Models;
using extgen.TypeSystem;
using extgen.TypeSystem.Cpp;

namespace extgen.Bridge.Objc
{

    /// <summary>
    /// Bridge abstraction for Apple platforms (ObjC vs Swift).
    /// Decides:
    ///   - how __impl ivar is declared
    ///   - how init configures implementation
    ///   - whether enums are decoded as typed enums or underlying scalars
    ///   - how to call the actual implementation (__impl)
    ///   - what the effective return IrType is (Swift enum bridge)
    /// </summary>
    internal interface IAppleBridge
    {
        void EmitWire(ObjcLayout layout);

        /// <summary>
        /// Emit the implementation ivar in the class extension.
        /// e.g. @interface ExtInternal () { id&lt;ExtInterface&gt; __impl; }
        /// or   ExtSwift* __impl;
        /// </summary>
        void EmitIvars(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w);

        /// <summary>
        /// Emit the body of -init, responsible for assigning __impl.
        /// </summary>
        void EmitInitBody(ObjcEmitterContext ctx, ObjcWriter body);

        /// <summary>
        /// Emit the body of a given function.
        /// </summary>
        void EmitMethodBody(ObjcEmitterContext ctx, ObjcWriter fnBody, IrFunction fn);

        void EmitHeaderArtifacts(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w);
        void EmitExtraHeaderDeclarations(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w, IIrTypeMap cppTypeMap);
        void EmitExtraImports(ObjcEmitterContext ctx, ObjcWriter w);
        void EmitUserInterface(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w, CppTypeMap cppTypeMap);

        void EmitInvocationHandlerMethod(ObjcEmitterContext ctx, ObjcWriter w);
        void EmitQueueBufferMethod(ObjcEmitterContext ctx, ObjcWriter w);

        public IEnumerable<string>? UserShellProtocols(ObjcEmitterContext ctx);
    }
}
