// Bridge/Java/JavaBridgeBase.cs
using codegencore.Writers.Lang;
using extgen.Emitters.Java;
using extgen.Emitters.Utils;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem;
using System.Collections.Immutable;

namespace extgen.Bridge.Java
{
    /// <summary>
    /// Base Java bridge: shared implementation for Java and Kotlin backends.
    /// Subclasses only override "flavor" bits like backing field and call target.
    /// </summary>
    internal abstract class JavaBridgeGenerator(
        IIrTypeMap types,
        RuntimeNaming runtime,
        JavaWireHelpers wireHelpers
    ) : BridgeGeneratorBase<JavaEmitterOptions, JavaWriter>(types, runtime)
    {
        protected JavaWireHelpers Wire { get; } = wireHelpers;

        // ---------- flavor hooks ----------

        /// <summary>
        /// The im
        /// </summary>
        public virtual string[]? GetClassImplements(IEmitterContext<JavaEmitterOptions> ctx)
        => [$"{ctx.ExtName}Interface"];

        /// <summary>
        /// Optional backing field (e.g. __kotlin_instance). Default = no-op.
        /// </summary>
        public override void EmitBackingField(IEmitterContext<JavaEmitterOptions> ctx, JavaWriter w)
        {
            // default: nothing
        }

        /// <summary>
        /// Expression used to call the user implementation for a given function.
        /// e.g. "fnName" (Java) or "__kotlin_instance.fnName" (Kotlin).
        /// </summary>
        protected abstract string GetTargetExpression(IEmitterContext<JavaEmitterOptions> ctx, IrFunction fn);

        // ---------- invocation handler & buffer queue are fully shared ----------

        public override void EmitInvocationHandler(
            IEmitterContext<JavaEmitterOptions> ctx,
            ImmutableArray<IrFunction> funcs,
            JavaWriter w)
        {
            var usesFunctions = funcs.Any(f => f.Parameters.Any(p => p.Type.Kind == IrTypeKind.Function));
            if (!usesFunctions)
                return;

            w.Field(
                type: $"{Runtime.WireClass}.DispatchQueue",
                name: Runtime.DispatchQueueField,
                initializer: $"new {Runtime.WireClass}.DispatchQueue()",
                modifiers: ["private", "final"]
            );

            w.Function(
                name: $"{Runtime.NativePrefix}{ctx.ExtName}_invocation_handler",
                parameters: [
                    new Param("ByteBuffer", Runtime.RetBufferParam),
                    new Param("double",    Runtime.RetBufferLengthParam)
                ],
                body: m => m.Return($"{Runtime.DispatchQueueField}.fetch({Runtime.RetBufferParam})"),
                returnType: "double",
                modifiers: ["public"]
            );

            w.Line();
        }

        public override void EmitBufferQueueHandler(
            IEmitterContext<JavaEmitterOptions> ctx,
            ImmutableArray<IrFunction> funcs,
            JavaWriter w)
        {
            var usesBuffers = funcs.Any(f => f.Parameters.Any(p => p.Type.Kind == IrTypeKind.Buffer));
            if (!usesBuffers)
                return;

            w.Field(
                type: "Deque<ByteBuffer>",
                name: Runtime.BufferQueueField,
                initializer: "new ArrayDeque<>()",
                modifiers: ["private", "final"]
            );

            w.Function(
                name: $"{Runtime.NativePrefix}{ctx.ExtName}_queue_buffer",
                parameters: [
                    new Param("ByteBuffer", Runtime.ArgBufferParam),
                    new Param("double",     Runtime.ArgBufferLengthParam)
                ],
                body: m =>
                {
                    m.Line($"{Runtime.BufferQueueField}.offer({Runtime.ArgBufferParam});");
                    m.Return("0");
                },
                returnType: "double",
                modifiers: ["public"]
            );

            w.Line();
        }

        // ---------- function bridge (__EXT_NATIVE__Foo) shared, with hooks ----------

        public override void EmitFunctionBridge(
            IEmitterContext<JavaEmitterOptions> ctx,
            IrFunction fn,
            JavaWriter w)
        {
            bool needArgsBuf = IrAnalysis.NeedsArgsBuffer(fn);
            bool needRetBuf = IrAnalysis.NeedsRetBuffer(fn);

            var ps = ExportTypeUtils.ParamsFor(fn, Runtime);
            string targetExpression = GetTargetExpression(ctx, fn);

            w.Function(
                name: $"{Runtime.NativePrefix}{fn.Name}",
                parameters: ps.AsJava(),
                body: m =>
                {
                    var callArgs = BuildCallArguments(m, fn, needArgsBuf);

                    EmitBeforeCall(ctx, fn, m);

                    if (fn.ReturnType.Kind != IrTypeKind.Void)
                    {
                        m.Assign(
                            Runtime.ResultVar,
                            lhs: x => x.Call(targetExpression, [.. callArgs]),
                            type: Types.Map(fn.ReturnType, owned: true)
                        );
                    }
                    else
                    {
                        m.Call(targetExpression, [.. callArgs]).Line(";");
                    }

                    EmitAfterCall(ctx, fn, needRetBuf, m);

                    JavaReturnType.EmitReturn(m, Runtime.ResultVar, fn);
                },
                returnType: JavaReturnType.ReturnFor(fn),
                modifiers: ["public"]
            ).Line();
        }

        /// <summary>
        /// Optional hook for code before the actual call (default: noop).
        /// </summary>
        protected virtual void EmitBeforeCall(IEmitterContext<JavaEmitterOptions> ctx, IrFunction fn, JavaWriter m)
        {
        }

        /// <summary>
        /// Optional hook for code after the call but before returning.
        /// Handles return-buffer encoding by default.
        /// </summary>
        protected virtual void EmitAfterCall(
            IEmitterContext<JavaEmitterOptions> ctx,
            IrFunction fn,
            bool needRetBuf,
            JavaWriter m)
        {
            if (!needRetBuf)
                return;

            m.Line();
            m.Call($"{Runtime.WireClass}.order", [Runtime.RetBufferParam]).Line(";");
            m.Comment($"return: {Runtime.ResultVar}, type: {fn.ReturnType.Name}{(fn.ReturnType.IsCollection ? $"[{fn.ReturnType.FixedLength}]" : string.Empty)}");
            Wire.EncodeLines(m, fn.ReturnType, Runtime.ResultVar, Runtime.RetBufferParam);
            m.Line();
        }

        // ---------- shared arg decoding ----------

        private List<string> BuildCallArguments(JavaWriter m, IrFunction fn, bool needArgsBuf)
        {
            var callArgs = new List<string>();

            if (needArgsBuf)
            {
                m.Call($"{Runtime.WireClass}.order", [Runtime.ArgBufferParam]).Line(";").Line();

                foreach (var p in fn.Parameters)
                {
                    m.Comment($"field: {p.Name}, type: {p.Type.Name}{(p.Type.IsCollection ? $"[{p.Type.FixedLength}]" : string.Empty)}");
                    Wire.DecodeLines(m, p.Type, p.Name, declare: true, bufferVar: Runtime.ArgBufferParam);
                    m.Line();
                    callArgs.Add(p.Name);
                }
            }
            else
            {
                foreach (var p in IrAnalysis.DirectArgs(fn))
                {
                    var t = p.Type;
                    var name = p.Name;

                    string expr;
                    if (t.IsNumericScalar)
                    {
                        var javaType = Types.Map(t, owned: true);

                        if (t.Name.Equals("bool", StringComparison.InvariantCultureIgnoreCase))
                        {
                            expr = $"{name} != 0";
                        }
                        else
                        {
                            expr = $"({javaType}){name}";
                        }
                    }
                    else if (t.IsStringScalar)
                    {
                        // Bridge type is String, and user type is String too.
                        expr = name;
                    }
                    else
                    {
                        // If you ever mark more cases as "direct", handle them here.
                        // For now, just pass directly (you can refine later).
                        expr = name;
                    }

                    callArgs.Add(expr);
                }
            }

            return callArgs;
        }
    }
}
