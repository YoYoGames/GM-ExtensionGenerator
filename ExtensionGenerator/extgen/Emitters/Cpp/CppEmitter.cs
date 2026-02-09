using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Emitters.Utils;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.TypeSystem.Cpp;
using extgen.Utils;

namespace extgen.Emitters.Cpp
{
    public sealed class CppEmitter(CppEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly CppTypeMap typeMap = new(runtime);

        public void Emit(IrCompilation comp, string dir)
        {
            var ctx = new CppEmitterContext(comp.Name, settings, runtime);
            var ext = comp.Name;

            var enums = new IrTypeEnumResolver(comp.Enums);

            var layout = new CppLayout(dir, settings);

            // 1) code gen files (always overwrite)
            EmitWire(layout.CoreDir);

            FileEmitHelpers.WriteCpp(layout.CodeGenDir, $"{ext}Internal_native.h", w => EmitInternalHeader(ctx, comp, enums, w));
            FileEmitHelpers.WriteCpp(layout.CodeGenDir, $"{ext}Internal_native.cpp", w => EmitInternalImpl(ctx, comp, enums, w));

            FileEmitHelpers.WriteCppIfMissing(layout.SourceDir, $"{string.Format(settings.SourceFilename, ext)}.h", w => EmitUserHeader(ctx, w));
            FileEmitHelpers.WriteCppIfMissing(layout.SourceDir, $"{string.Format(settings.SourceFilename, ext)}.cpp", w => EmitUserImpl(ctx, w));
        }

        public static void EmitWire(string destinationFolder) {
            Directory.CreateDirectory(destinationFolder);

            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cpp.GMExtWire.cpp", Path.Combine(destinationFolder, "GMExtWire.cpp"));
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cpp.GMExtWire.h", Path.Combine(destinationFolder, "GMExtWire.h"));
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cpp.GMExtUtils.cpp", Path.Combine(destinationFolder, "GMExtUtils.cpp"));
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Cpp.GMExtUtils.h", Path.Combine(destinationFolder, "GMExtUtils.h"));
        }

        // =====================================================================
        // 1. INTERNAL BASE: code_gen/{Ext}Internal.h / .cpp
        //    - holds the *current* implementation
        //    - exposes C entry points __EXT_NATIVE__...
        // =====================================================================

        private void EmitInternalHeader(CppEmitterContext ctx, IrCompilation c, IIrTypeEnumResolver enums, CppWriter w)
        {
            CppCommonEmitter<CppWriter> common = new(ctx, typeMap, enums);

            w.PragmaOnce();

            CppCommonEmitter<CppWriter>.EmitCommonIncludes(w);
            w.Include("core/GMExtWire.h", false).Line();
            common.EmitCommonCppArtifacts(w, c);

            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
            var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer)));

            if (usesFunctions)
            {
                w.Line($$"""
                // Internal function used for fetching dispatched function calls to GML
                GMEXPORT double {{ctx.Runtime.NativePrefix}}{{c.Name}}_invocation_handler(char* {{ctx.Runtime.RetBufferParam}}, double {{ctx.Runtime.RetBufferLengthParam}});
                """);
                w.Line();
            }

            if (usesBuffers)
            {
                w.Line($$"""
                // Internal function used for queueing buffers to native code
                GMEXPORT double {{ctx.Runtime.NativePrefix}}{{c.Name}}_queue_buffer(char* {{ctx.Runtime.ArgBufferParam}}, double {{ctx.Runtime.ArgBufferLengthParam}});
                """);
                w.Line();
            }

            // 1. internal code gen signatures
            foreach (var fn in c.Functions)
            {
                var exportName = $"{ctx.Runtime.NativePrefix}{fn.Name}";
                var ps = ExportTypeUtils.ParamsFor(fn, ctx.Runtime);
                w.FunctionDecl(exportName, ps.AsCpp(), ExportTypeUtils.ReturnFor(fn).AsCppType(), modifiers: ["GMEXPORT"]);
            }
            w.Line();

            // 2. clean user-side signatures
            foreach (var fn in c.Functions)
            {
                w.FunctionDecl($"{fn.Name}", fn.Parameters.Select(p => new Param(typeMap.MapPassType(p.Type), p.Name)), typeMap.Map(fn.ReturnType, true));
            }
        }

        private void EmitInternalImpl(CppEmitterContext ctx, IrCompilation c, IIrTypeEnumResolver enums, CppWriter w)
        {
            CppCommonEmitter<CppWriter> common = new(ctx, typeMap, enums);

            // Local includes
            w.Include($"{ctx.ExtName}Internal_native.h", false)
            .Line()
            .UsingNamespace(ctx.Runtime.StructsNamespace)
            .UsingNamespace(ctx.Runtime.CodeGenNamespace)
            .Line();

            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
            var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer)));

            if (usesFunctions)
            {
                w.Line($$"""
                static {{ctx.Runtime.RuntimeNamespace}}::DispatchQueue {{ctx.Runtime.DispatchQueueField}};

                // Internal function used for fetching dispatched function calls to GML
                GMEXPORT double {{ctx.Runtime.NativePrefix}}{{c.Name}}_invocation_handler(char* {{ctx.Runtime.RetBufferParam}}, double {{ctx.Runtime.RetBufferLengthParam}})
                {
                    {{ctx.Runtime.ByteIONamespace}}::BufferWriter {{ctx.Runtime.BufferWriterVar}}{ {{ctx.Runtime.RetBufferParam}}, static_cast<size_t>({{ctx.Runtime.RetBufferLengthParam}}) };
                    return {{ctx.Runtime.DispatchQueueField}}.fetch({{ctx.Runtime.BufferWriterVar}});
                }
                """);
                w.Line();
            }

            if (usesBuffers)
            {
                w.Line($$"""
                static std::queue<{{ctx.Runtime.ExtWireNamespace}}::GMBuffer> {{ctx.Runtime.BufferQueueField}};

                // Internal function used for queueing buffers to native code
                GMEXPORT double {{ctx.Runtime.NativePrefix}}{{c.Name}}_queue_buffer(char* {{ctx.Runtime.ArgBufferParam}}, double {{ctx.Runtime.ArgBufferLengthParam}})
                {
                    {{ctx.Runtime.ExtWireNamespace}}::GMBuffer __buff{{{ctx.Runtime.ArgBufferParam}}, static_cast<uint64_t>({{ctx.Runtime.ArgBufferLengthParam}})};
                    {{ctx.Runtime.BufferQueueField}}.push(__buff);
                
                    return 1.0;
                }
                """);
                w.Line();
            }

            foreach (var fn in c.Functions)
            {
                var needsArgBuffer = IrAnalysis.NeedsArgsBuffer(fn);
                var needsRetBuffer = IrAnalysis.NeedsRetBuffer(fn);
                var exportName = $"{ctx.Runtime.NativePrefix}{fn.Name}";

                var ps = ExportTypeUtils.ParamsFor(fn, ctx.Runtime);

                w.Function(exportName, ps.AsCpp(), funcBody =>
                {
                    var callArgs = common.EmitDecode(funcBody, fn, needsArgBuffer, ctx.Runtime.BufferReaderVar);

                    if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.String })
                    {
                        funcBody.Line($"static std::string {ctx.Runtime.ResultVar};");
                        funcBody.Assign(ctx.Runtime.ResultVar, e => e.Call(fn.Name, [.. callArgs]));
                    }
                    else if (!(fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.Void }))
                        funcBody.Assign(ctx.Runtime.ResultVar, e => e.Call(fn.Name, [.. callArgs]), "auto&&");
                    else
                        funcBody.Call(fn.Name, [.. callArgs]).Line(";");

                    common.EmitEncodeReturn(funcBody, fn.ReturnType, ctx.Runtime.ResultVar, needsRetBuffer, ctx.Runtime.BufferWriterVar);

                }, ExportTypeUtils.ReturnFor(fn).AsCppType(), modifiers: ["GMEXPORT"])
                .Line();
            }
        }

        // =====================================================================
        // 2. USER IMPLEMENTATION: src/{Ext}.h / src/{Ext}.cpp
        //    - file where implementation lies
        // =====================================================================

        private static void EmitUserHeader(CppEmitterContext ctx, CppWriter w)
        {
            w.Include($"native/{ctx.ExtName}Internal_native.h", false).Line();
        }

        private static void EmitUserImpl(CppEmitterContext ctx, CppWriter w) 
        {
            w.Include($"{string.Format(ctx.Settings.SourceFilename, ctx.ExtName)}.h", false).Line();

            w.UsingNamespace(ctx.Runtime.ExtWireNamespace);
            w.UsingNamespace(ctx.Runtime.StructsNamespace);
            w.UsingNamespace(ctx.Runtime.EnumsNamespace);
        }
    }
}
