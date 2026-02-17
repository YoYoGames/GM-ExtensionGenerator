using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge.Objc;
using extgen.Emitters.AppleMobile.Objc;
using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.TypeSystem.Objc;
using extgen.Utils;

namespace extgen.Emitters.AppleMobile.ObjcNative
{

    /// <summary>
    /// Minimal iOS/tvOS “framework” emitter.
    /// Generates:
    ///   ios/{Ext}.h  – C exports + @interface {Ext}
    ///   ios/{Ext}.mm – @implementation {Ext} that forwards 1:1
    ///
    /// IMPORTANT: ObjC method name == exported C name
    ///   - (double) __EXT_NATIVE__foo:(double)arg0 ... { return __EXT_NATIVE__foo(...); }
    /// </summary>
    internal sealed class ObjcNativeEmitter(IAppleMobileEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly ObjcTypeMap typeMap = new(runtime);

        public void Emit(IrCompilation comp, string dir)
        {
            ObjcEmitterContext ctx = new(comp.Name, settings, runtime);
            ObjcLayout layout = new(dir, settings);

            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(ObjcEmitterContext ctx, IrCompilation c, ObjcLayout layout)
        {
            var ext = ctx.ExtName;
            var platform = ctx.Settings.Platform;

            // 1) code gen files (always overwrite)
            FileEmitHelpers.WriteObjc(layout.CodeGenDir, $"{ext}Internal_{platform}.h", w => EmitInternalHeader(ctx, c, w));
            FileEmitHelpers.WriteObjc(layout.CodeGenDir, $"{ext}Internal_{platform}.mm", w => EmitInternalImpl(ctx, c, w));

            var enums = new IrTypeEnumResolver(c.Enums);
            ObjcBridge bridge = new(enums);

            ObjcCommonEmitter common = new(ctx, typeMap, bridge);
            common.EmitObjcUserShell(c, layout);
        }

        // =====================================================================
        // 1. INTERNAL BASE: code_gen/ios/{Ext}Internal_ios.h / .mm
        //    - holds the *current* implementation
        //    - exposes C entry points __EXT_NATIVE__...
        // =====================================================================

        private static void EmitInternalHeader(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            var ext = ctx.ExtName;

            w.Import("Foundation/Foundation.h", true)
             .Line();

            var usesFunctions = c.HasFunctionType();
            var usesBuffers = c.HasBufferType();

            // ObjC interface
            w.Interface($"{ext}Internal", body: iBody =>
            {
                var allFunctions = c.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
                foreach (var fn in allFunctions)
                {
                    string exportName = $"{ctx.Runtime.NativePrefix}{fn.Name}";

                    var ps = ExportTypeUtils.ParamsFor(fn, new());
                    var ret = ExportTypeUtils.ReturnFor(fn).AsCppType();

                    iBody.MethodDecl(false, ret, exportName, [.. ps.AsObjc()]);
                }

                if (usesFunctions)
                {
                    iBody.MethodDecl(false, "double", $"{ctx.Runtime.NativePrefix}{ext}_invocation_handler", [new("", "char*", ctx.Runtime.RetBufferParam), new("arg1", "double", ctx.Runtime.RetBufferLengthParam)]);
                }

                if (usesBuffers)
                {
                    iBody.MethodDecl(false, "double", $"{ctx.Runtime.NativePrefix}{ext}_queue_buffer", [new("", "char*", ctx.Runtime.ArgBufferParam), new("arg1", "double", ctx.Runtime.ArgBufferLengthParam)]);
                }
            });
        }

        private static void EmitInternalImpl(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            var ext = ctx.ExtName;
            var platform = ctx.Settings.Platform;

            w.Import($"{ext}Internal_{platform}.h")
             .Import($"native/{ext}Internal_native.h")
             .Line();

            var usesFunctions = c.HasFunctionType();
            var usesBuffers = c.HasBufferType();

            w.Implementation($"{ext}Internal", implBody =>
            {
                var allFunctions = c.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
                foreach (var fn in allFunctions) 
                {
                    string exportName = $"{ctx.Runtime.NativePrefix}{fn.Name}";

                    var ps = ExportTypeUtils.ParamsFor(fn, new());
                    var ret = ExportTypeUtils.ReturnFor(fn).AsCppType();

                    w.Method(false, ret, exportName, [.. ps.AsObjc()], fnBody =>
                    {
                        w.Return(expr => expr.Call(exportName, [.. ps.Select(p => p.Name)]));
                    });
                }

                if (usesFunctions) 
                {
                    var bufferParam = ctx.Runtime.ArgBufferParam;
                    var bufferLengthParam = ctx.Runtime.ArgBufferLengthParam;

                    string exportName = $"{ctx.Runtime.NativePrefix}{ext}_invocation_handler";
                    w.Method(false, "double", exportName, [new("", "char*", bufferParam), new("arg1", "double", bufferLengthParam)], fnBody =>
                    {
                        w.Return(expr => expr.Call(exportName, bufferParam, bufferLengthParam));
                    });
                }

                if (usesBuffers) 
                {
                    var bufferParam = ctx.Runtime.ArgBufferParam;
                    var bufferLengthParam = ctx.Runtime.ArgBufferLengthParam;

                    string exportName = $"{ctx.Runtime.NativePrefix}{ext}_queue_buffer";
                    w.Method(false, "double", exportName, [new("", "char*", bufferParam), new("arg1", "double", bufferLengthParam)], fnBody =>
                    {
                        w.Return(expr => expr.Call(exportName, bufferParam, bufferLengthParam));
                    });
                }
            });
        }
    }
}
