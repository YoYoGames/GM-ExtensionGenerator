using extgen.Emitters.Utils;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem;
using System.Collections.Immutable;

namespace extgen.Bridge
{
    internal interface IBridgeGenerator<TOptions, TWriter>
    {
        void EmitBackingField(IEmitterContext<TOptions> ctx, TWriter w);

        void EmitInvocationHandler(IEmitterContext<TOptions> ctx,
                                   ImmutableArray<IrFunction> funcs,
                                   TWriter w);

        void EmitBufferQueueHandler(IEmitterContext<TOptions> ctx,
                                    ImmutableArray<IrFunction> funcs,
                                    TWriter w);

        void EmitFunctionBridge(IEmitterContext<TOptions> ctx, IrFunction fn, TWriter w);
    }

    internal abstract class BridgeGeneratorBase<TOptions, TWriter>(
        IIrTypeMap types,
        RuntimeNaming runtime
    ) : IBridgeGenerator<TOptions, TWriter>
    {
        protected readonly IIrTypeMap Types = types;
        protected readonly RuntimeNaming Runtime = runtime;

        public abstract void EmitBackingField(IEmitterContext<TOptions> ctx, TWriter w);
        public abstract void EmitInvocationHandler(IEmitterContext<TOptions> ctx, ImmutableArray<IrFunction> funcs, TWriter w);
        public abstract void EmitBufferQueueHandler(IEmitterContext<TOptions> ctx, ImmutableArray<IrFunction> funcs, TWriter w);
        public abstract void EmitFunctionBridge(IEmitterContext<TOptions> ctx, IrFunction fn, TWriter w);
    }
}
