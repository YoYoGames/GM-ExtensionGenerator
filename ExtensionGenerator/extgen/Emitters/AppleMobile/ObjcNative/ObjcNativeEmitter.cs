using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge.ObjcNative;
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
    /// Emits minimal iOS/tvOS native framework code with Objective-C wrappers that forward to C exports.
    /// ObjC method names match exported C function names for 1:1 forwarding.
    /// </summary>
    internal sealed class ObjcNativeEmitter(IAppleMobileEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly ObjcTypeMap typeMap = new(runtime);

        /// <summary>
        /// Emits the Objective-C native implementation for the given compilation.
        /// </summary>
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

            ObjcNativeBridge bridge = new(enums);
            ObjcCommonEmitter common = new(ctx, typeMap, bridge);
            common.EmitObjcUserShell(c, layout);
        }

        private static void EmitInternalHeader(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            var ext = ctx.ExtName;

            w.Comment("##### extgen :: Auto-generated file do not edit!! #####").Line();
            w.Import("Foundation/Foundation.h", true)
             .Line();

            var usesFunctions = c.HasFunctionType();
            var usesBuffers = c.HasBufferType();

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

            w.Comment("##### extgen :: Auto-generated file do not edit!! #####").Line();
            w.Import($"{ext}Internal_{platform}.h")
             .Import($"native/{ext}Internal_exports.h")
             .Import("objc/runtime.h", true)
             .Line();

            w.Lines("""

                extern "C" const char* extOptGetString(char* _ext, char* _opt);

                // Adapter: matches const signature expected by your C++ API
                static const char* ExtOptGetString(const char* ext, const char* opt)
                {
                    return extOptGetString(const_cast<char*>(ext), const_cast<char*>(opt));
                }

                static BOOL GMIsSubclassOf(Class cls, Class base)
                {
                    for (Class c = cls; c != Nil; c = class_getSuperclass(c)) {
                        if (c == base) return YES;
                    }
                    return NO;
                }

                static void GMInjectSelectorsIntoSubclass(Class subclass, Class base)
                {
                    // Build set of methods already defined on subclass
                    unsigned subCount = 0;
                    Method *subList = class_copyMethodList(subclass, &subCount);

                    CFMutableSetRef owned = CFSetCreateMutable(kCFAllocatorDefault, 0, NULL);
                    for (unsigned i = 0; i < subCount; ++i) {
                        CFSetAddValue(owned, method_getName(subList[i]));
                    }

                    // Walk base class methods
                    unsigned baseCount = 0;
                    Method *baseList = class_copyMethodList(base, &baseCount);

                    for (unsigned i = 0; i < baseCount; ++i) {
                        SEL sel = method_getName(baseList[i]);
                        const char *name = sel_getName(sel);

                        // Only inject your extension selectors
                        if (!name || strncmp(name, "__EXT_NATIVE__", 13) != 0) continue;

                        // Add only if subclass doesn't already have it
                        if (!CFSetContainsValue(owned, sel)) {
                            IMP imp = method_getImplementation(baseList[i]);
                            const char *types = method_getTypeEncoding(baseList[i]);
                            if (class_addMethod(subclass, sel, imp, types)) {
                                CFSetAddValue(owned, sel);
                            }
                        }
                    }

                    if (subList) free(subList);
                    if (baseList) free(baseList);
                    if (owned) CFRelease(owned);
                }

                """);

            var usesFunctions = c.HasFunctionType();
            var usesBuffers = c.HasBufferType();

            w.Implementation($"{ext}Internal", implBody =>
            {
                implBody.Lines($$"""

                    + (void)load
                    {
                        // Find all loaded classes
                        int num = objc_getClassList(NULL, 0);
                        if (num <= 0) return;

                        Class *classes = (Class *)malloc(sizeof(Class) * (unsigned)num);
                        num = objc_getClassList(classes, num);

                        Class base = [{{ext}}Internal class];

                        for (int i = 0; i < num; ++i) {
                            Class cls = classes[i];
                            if (cls == base) continue;

                            // We only care about direct or indirect subclasses
                            if (GMIsSubclassOf(cls, base)) {
                                GMInjectSelectorsIntoSubclass(cls, base);
                            }
                        }

                        free(classes);

                        gm::details::GMRTRunnerInterface ri{};
                        ri.ExtOptGetString = &ExtOptGetString;
                        GMExtensionInitialise(&ri, sizeof(ri));
                    }

                    """);

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
