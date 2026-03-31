using extgen.Emitters.Utils;
using extgen.Models;
using extgen.Models.Config;
using extgen.TypeSystem;
using System.Collections.Immutable;

namespace extgen.Bridge
{
    /// <summary>
    /// Interface for bridge code generators that connect GameMaker runtime to native implementations.
    /// </summary>
    internal interface IBridgeGenerator<TOptions, TWriter>
    {
        /// <summary>Emits backing field declarations for the implementation.</summary>
        void EmitBackingField(IEmitterContext<TOptions> ctx, TWriter w);

        /// <summary>Emits the invocation handler that dispatches calls to native code.</summary>
        void EmitInvocationHandler(IEmitterContext<TOptions> ctx,
                                   ImmutableArray<IrFunction> funcs,
                                   TWriter w);

        /// <summary>Emits the buffer queue handler for async operations.</summary>
        void EmitBufferQueueHandler(IEmitterContext<TOptions> ctx,
                                    ImmutableArray<IrFunction> funcs,
                                    TWriter w);

        /// <summary>Emits a bridge wrapper for a single function.</summary>
        void EmitFunctionBridge(IEmitterContext<TOptions> ctx, IrFunction fn, TWriter w);
    }

    /// <summary>
    /// Base class for bridge code generators.
    /// Provides common infrastructure and type mapping for language-specific implementations.
    /// </summary>
    internal abstract class BridgeGeneratorBase<TOptions, TWriter>(
        IIrTypeMap types,
        RuntimeNaming runtime
    ) : IBridgeGenerator<TOptions, TWriter>
    {
        /// <summary>Type mapping for the target language.</summary>
        protected readonly IIrTypeMap Types = types;

        /// <summary>Runtime naming conventions.</summary>
        protected readonly RuntimeNaming Runtime = runtime;

        /// <inheritdoc />
        public abstract void EmitBackingField(IEmitterContext<TOptions> ctx, TWriter w);

        /// <inheritdoc />
        public abstract void EmitInvocationHandler(IEmitterContext<TOptions> ctx, ImmutableArray<IrFunction> funcs, TWriter w);

        /// <inheritdoc />
        public abstract void EmitBufferQueueHandler(IEmitterContext<TOptions> ctx, ImmutableArray<IrFunction> funcs, TWriter w);

        /// <inheritdoc />
        public abstract void EmitFunctionBridge(IEmitterContext<TOptions> ctx, IrFunction fn, TWriter w);
    }
}
