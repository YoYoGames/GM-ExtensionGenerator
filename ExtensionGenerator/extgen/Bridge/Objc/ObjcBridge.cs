using codegencore.Writers.Lang;
using extgen.Emitters.Cpp;
using extgen.Emitters.Objc;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem;
using extgen.TypeSystem.Cpp;

namespace extgen.Bridge.Objc
{
    /// <summary>
    /// ObjC flavor:
    ///   - typed enums
    ///   - implementation is (id&lt;ExtInterface&gt;)self
    ///   - calls __impl via ObjC message send.
    /// </summary>
    internal sealed class ObjcBridge : IAppleBridge
    {
        public void EmitIvars(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            var ext = ctx.ExtName;
            var impl = ctx.Runtime.ImplField;
            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.Kind == IrTypeKind.Function));
            var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.Kind == IrTypeKind.Buffer));

            if (usesFunctions)
                w.IVar($"{ctx.Runtime.RuntimeNamespace}::DispatchQueue", ctx.Runtime.DispatchQueueField);

            if (usesBuffers)
                w.IVar($"std::queue<{ctx.Runtime.ExtWireNamespace}::GMBuffer>", ctx.Runtime.BufferQueueField);

            w.IVar($"id<{ext}Interface>", impl);
        }

        public void EmitInitBody(ObjcEmitterContext ctx, ObjcWriter body)
        {
            var ext = ctx.ExtName;
            var impl = ctx.Runtime.ImplField;
            // For ObjC we just point __impl at self (which implements ExtInterface)
            body.Line($"{impl} = (id<{ext}Interface>)self;");
        }

        public void EmitMethodBody(ObjcEmitterContext ctx, ObjcWriter fnBody, IrFunction fn)
        {
            var cppTypeMap = new CppTypeMap(ctx.Runtime);

            CppEmitterContext cppCtx = new(ctx.ExtName, new CppEmitterOptions(), ctx.Runtime);
            CppCommonEmitter<ObjcWriter> commmon = new(cppCtx, cppTypeMap);

            // 1) decode (reused helper)
            var needsArgBuffer = IrAnalysis.NeedsArgsBuffer(fn);
            var callArgs = commmon.EmitDecode(fnBody, fn, needsArgBuffer, ctx.Runtime.BufferReaderVar);

            var callArgsLabels = fn.Parameters.Zip(callArgs).Select(z => ((string, string))(z.First.Name, z.Second));

            var impl = ctx.Runtime.ImplField;
            var returnType = fn.ReturnType;

            if (returnType.Kind != IrTypeKind.Void)
            {
                var cppRet = cppTypeMap.Map(fn.ReturnType, owned: true);
                var resultVar = ctx.Runtime.ResultVar;

                if (returnType.IsStringScalar)
                {
                    fnBody.Line($"static std::string {resultVar};");
                    fnBody.Assign(resultVar, e => e.MsgSend(impl, fn.Name, callArgsLabels.ToList()));
                }
                else
                {
                    fnBody.Assign(resultVar, e => e.MsgSend(impl, fn.Name, callArgsLabels.ToList()), cppRet);
                }
            }
            else
            {
                fnBody.MsgSend(impl, fn.Name, callArgsLabels.ToList()).Line(";");
            }

            fnBody.Line();

            // 3) encode/return (reused helper)
            var needsRetBuffer = IrAnalysis.NeedsRetBuffer(fn);
            commmon.EmitEncodeReturn(fnBody, returnType!, ctx.Runtime.ResultVar, needsRetBuffer, ctx.Runtime.BufferWriterVar);
        }

        public void EmitHeaderArtifacts(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            CppCommonEmitter<ObjcWriter>.EmitCommonIncludes(w);
            w.Include("GMExtWire.h", false).Line();

            CppEmitterContext cppCtx = new(ctx.ExtName, new CppEmitterOptions(), ctx.Runtime);
            CppCommonEmitter<ObjcWriter> commmon = new(cppCtx, new CppTypeMap(ctx.Runtime));
            commmon.EmitCommonCppArtifacts(w, c);
        }

        public void EmitExtraHeaderDeclarations(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w, IIrTypeMap cppTypeMap)
        {
            // Emit ExtInterface protocol
            var ext = ctx.ExtName;

            w.Protocol($"{ext}Interface", ["NSObject"], body =>
            {
                foreach (var fn in c.Functions)
                {
                    w.MethodDecl(
                        false,
                        cppTypeMap.Map(fn.ReturnType, owned: true),
                        fn.Name,
                        [.. fn.Parameters.Select(p =>
                        new ObjcParam(p.Name, cppTypeMap.MapPassType(p.Type), p.Name))]);
                }
            }).Line();
        }

        public void EmitExtraImports(ObjcEmitterContext ctx, ObjcWriter w)
        {
            // Do nothing on Objc++
        }

        public void EmitUserInterface(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w, CppTypeMap cppTypeMap)
        {
            var ext = ctx.ExtName;

            w.Import("Foundation/Foundation.h", true)
             .Import($"{ext}Internal.h")
             .Line();

            w.Interface(ext, $"{ext}Internal", [$"{ext}Interface"], null).Line();
        }

        public void EmitInvocationHandlerMethod(ObjcEmitterContext ctx, ObjcWriter w)
        {
            var bufferParam = ctx.Runtime.RetBufferParam;
            var bufferLengthParam = ctx.Runtime.RetBufferLengthParam;

            w.Method(false, "double", $"{ctx.Runtime.NativePrefix}{ctx.ExtName}_invocation_handler",
            [new("", "char*", bufferParam), new("arg1", "double", bufferLengthParam)],
            fnBody =>
            {
                fnBody.Line($"{ctx.Runtime.ByteIONamespace}::BufferWriter {ctx.Runtime.BufferWriterVar}{{ {bufferParam}, static_cast<size_t>({bufferLengthParam}) }};");
                fnBody.Return($"{ctx.Runtime.DispatchQueueField}.fetch({ctx.Runtime.BufferWriterVar})");
            });
            w.Line();
        }

        public void EmitQueueBufferMethod(ObjcEmitterContext ctx, ObjcWriter w)
        {
            var bufferParam = ctx.Runtime.ArgBufferParam;
            var bufferLengthParam = ctx.Runtime.ArgBufferLengthParam;

            w.Method(false, "double", $"{ctx.Runtime.NativePrefix}{ctx.ExtName}_queue_buffer",
            [new("", "char*", bufferParam), new("arg1", "double", bufferLengthParam)],
            fnBody =>
            {
                fnBody.Line($"{ctx.Runtime.ExtWireNamespace}::GMBuffer __buff{{ {bufferParam}, static_cast<uint64_t>({bufferLengthParam}) }};");
                fnBody.Call($"{ctx.Runtime.BufferQueueField}.push", "__buff").Line(";");
                fnBody.Return("1.0");
            });
            w.Line();
        }

        public IEnumerable<string>? UserShellProtocols(ObjcEmitterContext ctx) => [$"{ctx.ExtName}Interface"];
    }
}
