using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge.Objc;
using extgen.Emitters.AppleMobile.Objc;
using extgen.Emitters.Utils;
using extgen.Models;
using extgen.TypeSystem;
using extgen.TypeSystem.Cpp;
using extgen.Utils;

namespace extgen.Bridge.Swift
{
    /// <summary>
    /// Swift flavor:
    ///   - enums decoded as underlying integer (TypedEnums = false)
    ///   - call __impl->fn or __impl->__bridge_fn when enums are involved
    ///   - effective return type for enum-returning functions is underlying scalar.
    /// </summary>
    internal sealed class SwiftBridge : IAppleBridge
    {
        public void EmitWire(ObjcLayout layout)
        {
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Swift.GMExtWire.swift", Path.Combine(layout.CodeGenDir, "GMExtWire.swift"));
        }

        public void EmitIvars(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            var ext = ctx.ExtName;
            var impl = ctx.Runtime.ImplField;
            w.IVar($"{ext}Swift *", impl);
        }

        public void EmitInitBody(ObjcEmitterContext ctx, ObjcWriter body)
        {
            var ext = ctx.ExtName;
            body.IfDef("__cplusplus", then =>
            {
                then.Comment("Create Swift object once");
                then.Line($"__impl = new {ext}Swift({ext}Swift::init());");
            });
        }

        public void EmitMethodBody(ObjcEmitterContext ctx, ObjcWriter fnBody, IrFunction fn)
        {
            var ps = ExportTypeUtils.ParamsFor(fn, ctx.Runtime);
            var callArgs = ps.Select(p => p.Name);

            var implField = ctx.Runtime.ImplField;
            var resultVar = ctx.Runtime.ResultVar;
            var returnType = fn.ReturnType;
            var targetName = $"{ctx.Runtime.SwiftPrefix}{fn.Name}";

            if (!(returnType is IrType.Builtin { Kind: BuiltinKind.Void }))
            {
                if (returnType is IrType.Builtin { Kind: BuiltinKind.String })
                {
                    fnBody.Line($"static std::string {resultVar};");
                    fnBody.Assign(resultVar, e => e.Call($"(std::string){implField}->{targetName}", [.. callArgs]));
                    fnBody.Return($"(char*){resultVar}.c_str()");

                }
                else
                {
                    fnBody.Assign(resultVar, e => e.Call($"{implField}->{targetName}", [.. callArgs]), "double");
                    fnBody.Return(resultVar);
                }
            }
            else
            {
                fnBody.Call($"{implField}->{targetName}", [.. callArgs]).Line(";");
                fnBody.Return("0");
            }
        }

        public void EmitHeaderArtifacts(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            // Swift uses ObjC Internal as an implementation detail, but user Swift APIs
            // are generated in SwiftEmitter; Objc side doesn't need an extra artifacts.
            // No-op here.
        }

        public void EmitExtraHeaderDeclarations(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w, IIrTypeMap cppTypeMap)
        {
            // Swift uses ObjC Internal as an implementation detail, but user Swift APIs
            // are generated in SwiftEmitter; Objc side doesn't need an extra header declarations.
            // No-op here.
        }

        public void EmitExtraImports(ObjcEmitterContext ctx, ObjcWriter w)
        {
            w.IfDef("__cplusplus", then =>
            {
                then.Import("${YYXCodeProjName}-Swift.h");
                then.Line($"using namespace ${{YYXCodeProjName}};");
            }).Line();
        }

        public void EmitUserInterface(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w, CppTypeMap cppTypeMap)
        {
            // Swift uses ObjC Internal as an implementation detail, but user Swift APIs
            // are generated in SwiftEmitter; Objc side doesn't need an extra user header.
            // No-op here.
        }

        public void EmitInvocationHandlerMethod(ObjcEmitterContext ctx, ObjcWriter w) 
        {
            var extName = ctx.ExtName;
            var bufferParam = ctx.Runtime.RetBufferParam;
            var bufferLengthParam = ctx.Runtime.RetBufferLengthParam;

            w.Method(false, "double", $"{ctx.Runtime.NativePrefix}{extName}_invocation_handler",
            [new("", "char*", bufferParam), new("arg1", "double", bufferLengthParam)],
            fnBody =>
            {
                fnBody.Return($"{ctx.Runtime.ImplField}->{ctx.Runtime.SwiftPrefix}{extName}_invocation_handler({bufferParam}, {bufferLengthParam})");
            });
            w.Line();
        }

        public void EmitQueueBufferMethod(ObjcEmitterContext ctx, ObjcWriter w) 
        {
            var extName = ctx.ExtName;
            var bufferParam = ctx.Runtime.ArgBufferParam;
            var bufferLengthParam = ctx.Runtime.ArgBufferLengthParam;

            w.Method(false, "double", $"{ctx.Runtime.NativePrefix}{extName}_queue_buffer",
            [new("", "char*", bufferParam), new("arg1", "double", bufferLengthParam)],
            fnBody =>
            {
                fnBody.Return($"{ctx.Runtime.ImplField}->{ctx.Runtime.SwiftPrefix}{extName}_queue_buffer({bufferParam}, {bufferLengthParam})");
            });
            w.Line();
        }

        public IEnumerable<string>? UserShellProtocols(ObjcEmitterContext ctx) => null;
    }
}
