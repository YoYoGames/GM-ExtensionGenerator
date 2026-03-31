using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge.Objc;
using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Utils;
using extgen.TypeSystem;
using extgen.Utils;

namespace extgen.Emitters.AppleMobile.Objc
{
    internal enum AppleOSTargetKind { Objc, Swift }

    /// <summary>
    /// Emits Objective-C code for Apple mobile platforms (iOS, tvOS).
    /// Generates internal bridge files and user shell code.
    /// </summary>
    internal class ObjcCommonEmitter(ObjcEmitterContext ctx, IIrTypeMap cppTypeMap, IAppleBridge bridge)
    {
        // INTERNAL BASE - {Ext}Internal.h / .mm
        // Holds the current implementation and exposes Objc class entry points __EXT_NATIVE__

        /// <summary>
        /// Emits the internal bridge header and implementation files for the extension.
        /// </summary>
        public void EmitInternal(IrCompilation c, ObjcLayout layout) 
        {
            var platform = ctx.Settings.Platform;

            bridge.EmitWire(ctx, layout);

            FileEmitHelpers.WriteObjc(layout.CodeGenDir, $"{c.Name}Internal_{platform}.h", w => EmitInternalHeader(ctx, c, w));
            FileEmitHelpers.WriteObjc(layout.CodeGenDir, $"{c.Name}Internal_{platform}.mm", w => EmitInternalImpl(ctx, c, w));
        }

        /// <summary>
        /// Emits user-editable shell header and implementation files if they don't already exist.
        /// </summary>
        public void EmitObjcUserShell(IrCompilation c, ObjcLayout layout)
        {
            var ext = ctx.ExtName;
            var options = ctx.Settings;
            var platform = ctx.Settings.Platform;

            FileEmitHelpers.WriteObjcIfMissing(layout.SourceDir, $"{string.Format(options.SourceFilename, ext)}.h", w => EmitUserHeader(ctx, w));
            FileEmitHelpers.WriteObjcIfMissing(layout.SourceDir, $"{string.Format(options.SourceFilename, ext)}.mm", w => EmitUserImpl(ctx, w));
        }

        // Internal bridge (shared - switchable call target)

        private void EmitInternalHeader(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            var ext = c.Name;

            w.Comment("##### extgen :: Auto-generated file do not edit!! #####").Line();
            w.PragmaOnce()
                .Import("Foundation/Foundation.h", true)
                .Line();

            // Bridge-specific artifacts (ObjC enums, structs, codecs)
            bridge.EmitHeaderArtifacts(ctx, c, w);

            // Bridge-specific header extras (ObjC protocol, etc)
            bridge.EmitExtraHeaderDeclarations(ctx, c, w, cppTypeMap);

            var usesFunctions = c.HasFunctionType();
            var usesBuffers = c.HasBufferType();

            // 1. internal code gen signatures
            w.Interface($"{ext}Internal", body: body =>
            {
                var allFunctions = c.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
                foreach (var fn in allFunctions)
                {
                    string methodName = $"{ctx.Runtime.NativePrefix}{fn.Name}";
                    var ps = ExportTypeUtils.ParamsFor(fn, ctx.Runtime);
                    body.MethodDecl(false, ExportTypeUtils.ReturnFor(fn).AsCppType(), methodName, [.. ps.AsObjc()]);
                }

                if (usesFunctions)
                {
                    body.MethodDecl(false, "double", $"{ctx.Runtime.NativePrefix}{ext}_invocation_handler", [new("", "char*", ctx.Runtime.RetBufferParam), new("arg1", "double", ctx.Runtime.RetBufferLengthParam)]);
                }

                if (usesBuffers)
                {
                    body.MethodDecl(false, "double", $"{ctx.Runtime.NativePrefix}{ext}_queue_buffer", [new("", "char*", ctx.Runtime.ArgBufferParam), new("arg1", "double", ctx.Runtime.ArgBufferLengthParam)]);
                }

            }).Line();
        }

        private void EmitInternalImpl(ObjcEmitterContext ctx, IrCompilation c, ObjcWriter w)
        {
            var ext = c.Name;
            var platform = ctx.Settings.Platform;

            w.Comment("##### extgen :: Auto-generated file do not edit!! #####").Line()
                .Import("objc/runtime.h", true)
                .Import($"core/GMExtUtils.h")
                .Import($"{ext}Internal_{platform}.h")
                .Line();

            // Bridge-specific imports (#import Swift.h + namespace, or nothing)
            bridge.EmitExtraImports(ctx, w);

            // Injector helper tools
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

            w.ClassExtension($"{ext}Internal", extInt =>
            {
                // Ivars are bridge-specific
                bridge.EmitIvars(ctx, c, extInt);

            }).Line();

            // ObjC class implementation
            w.Implementation($"{ext}Internal", impBody =>
            {
                // Inject functions into subclasses
                impBody.Lines($$"""

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

                impBody.InitMethod(initBody =>
                {
                    // Bridge-specific init (assign __impl)
                    bridge.EmitInitBody(ctx, initBody);
                });

                var allFunctions = c.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
                foreach (var fn in allFunctions)
                {
                    var ps = ExportTypeUtils.ParamsFor(fn, ctx.Runtime);
                    var ret = ExportTypeUtils.ReturnFor(fn).AsCppType();

                    impBody.Method(false, ret, $"{ctx.Runtime.NativePrefix}{fn.Name}", [.. ps.AsObjc()], fnBody =>
                    {
                        bridge.EmitMethodBody(ctx, fnBody, fn);
                    })
                    .Line();
                }

                var usesFunctions = c.HasFunctionType();
                var usesBuffers = c.HasBufferType();

                if (usesFunctions)
                {
                    // invocation handler as INSTANCE method
                    impBody.Comment("Internal function used for fetching dispatched function calls to GML");
                    bridge.EmitInvocationHandlerMethod(ctx, impBody);
                }

                if (usesBuffers)
                {
                    // queue buffer as INSTANCE method
                    impBody.Comment("Internal function used for queueing buffers to native code");
                    bridge.EmitQueueBufferMethod(ctx, impBody);
                }
            });
        }

        // Public user shell
        private void EmitUserHeader(ObjcEmitterContext ctx, ObjcWriter w)
        {
            var ext = ctx.ExtName;
            var platform = ctx.Settings.Platform;

            w.Import("Foundation/Foundation.h", true)
                .Import($"{platform}/{ext}Internal_{platform}.h")
                .Line();

            w.Interface(ext, $"{ext}Internal", bridge.UserShellProtocols(ctx), null)
                .Line();
        }

        private static void EmitUserImpl(ObjcEmitterContext ctx, ObjcWriter w)
        {
            var ext = ctx.ExtName;
            var platform = ctx.Settings.Platform;

            w.Import($"{ext}_{platform}.h")
                .Line();

            w.Implementation(ext, (_) => { });
        }

    }
}
