using codegencore.Writers.Lang;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.Utils;

namespace extgen.Emitters.CppInjectors
{
    /// <summary>
    /// Emits C++ injector files for runtime integration with GameMaker.
    /// </summary>
    public sealed class CppInjectorsEmitter(CppInjectorsEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        /// <summary>
        /// Emits the C++ injector implementation for the given compilation.
        /// </summary>
        public void Emit(IrCompilation comp, string outputDir)
        {
            var destDir = Path.GetFullPath(Path.Combine(outputDir, settings.OutputFolder));
            Directory.CreateDirectory(destDir);

            var allFunctions = comp.GetAllFunctions(IrFunctionUtil.PatchStructMethod);

            var ctx = new CppInjectorsEmitterContext(
                Compilation: comp,
                Settings: settings,
                Runtime: runtime,
                StartFunctions: [.. allFunctions.Where(f => f.Modifier == IrFunctionModifier.Start)],
                FinishFunctions: [.. allFunctions.Where(f => f.Modifier == IrFunctionModifier.Finish)]
            );

            FileEmitHelpers.WriteCpp(destDir, "gmlib_injection_global_before_stubs.cpp", w => EmitGlobalBeforeStubs(ctx, w));
            FileEmitHelpers.WriteCpp(destDir, "gmlib_injection_global_after_stubs.cpp", w => EmitGlobalAfterStubs(ctx, w));
            FileEmitHelpers.WriteCpp(destDir, "gmlib_injection_setup_function.cpp", w => EmitSetupFunction(ctx, w));
            FileEmitHelpers.WriteCpp(destDir, "gmlib_injection_release_function.cpp", w => EmitReleaseFunction(ctx, w));
        }
        private void EmitGlobalBeforeStubs(CppInjectorsEmitterContext ctx, CppWriter w)
        {
            w.Line("static bool isInitialized = false;");
        }

        private void EmitGlobalAfterStubs(CppInjectorsEmitterContext ctx, CppWriter w)
        {
            var name = ctx.Compilation.Name;

            w.Struct("GMRTRunnerInterface", structBody => {
                structBody.Line("const char* (*ExtOptGetString)(const char* _ext, const char* _opt);");
            });

            var filename = settings.ExtensionFileName ?? $"{name}.ext";

            w.Function($"Init_{name}", [], fncBody =>
            {
                fncBody.Lines($$"""
                    GMRTRunnerInterface ri;
                    ri.ExtOptGetString = &ExtensionOptions_GetValue;

                    """);

                fncBody.Line("using FunctionPointer = void (*)(GMRTRunnerInterface*, size_t);");
                fncBody.Declare("FunctionPointer", "fnHandle", "nullptr");
                fncBody.Declare("void*", "libHandle", "nullptr");
                fncBody.Line();

                fncBody.Assign("libHandle", $"ExtUtils_GetLibraryHandle(\"{filename}\")");
                fncBody.If("libHandle", ifBody =>
                {
                    ifBody.Assign("fnHandle", $"(FunctionPointer)SharedLibrary_GetFunctionAddress(libHandle, \"GMExtensionInitialise\")");
                    ifBody.Line("if (fnHandle) fnHandle(&ri, sizeof(GMRTRunnerInterface));");
                });
            });

            w.Function($"Startup_{name}", [], fncBody => 
            {
                fncBody.Line("if (isInitialized) return;").Line();

                fncBody.Line("using FunctionPointer = void (*)();");
                fncBody.Declare("FunctionPointer", "fnHandle", "nullptr");
                fncBody.Declare("void*", "libHandle", "nullptr");
                fncBody.Line();

                foreach (var fnc in ctx.StartFunctions) {
                    fncBody.Assign("libHandle", $"ExtUtils_GetLibraryHandle(\"{filename}\")");
                    fncBody.If("libHandle", ifBody => 
                    {
                        var funcName = $"{runtime.NativePrefix}{fnc.Name}";
                        ifBody.Assign("fnHandle", $"(FunctionPointer)SharedLibrary_GetFunctionAddress(libHandle, \"{funcName}\")");
                        ifBody.Line("if (fnHandle) fnHandle();");
                    });
                }

                fncBody.Assign("isInitialized", "true");
            });

            w.Function($"Shutdown_{name}", [], fncBody =>
            {
                fncBody.Line("if (!isInitialized) return;").Line();

                fncBody.Line("using FunctionPointer = void (*)();");
                fncBody.Declare("FunctionPointer", "fnHandle", "nullptr");
                fncBody.Declare("void*", "libHandle", "nullptr");
                fncBody.Line();

                foreach (var fnc in ctx.FinishFunctions)
                {
                    fncBody.Assign("libHandle", $"ExtUtils_GetLibraryHandle(\"{filename}\")");
                    fncBody.If("libHandle", ifBody =>
                    {
                        var funcName = $"{runtime.NativePrefix}{fnc.Name}";
                        ifBody.Assign("fnHandle", $"(FunctionPointer)SharedLibrary_GetFunctionAddress(libHandle, \"{funcName}\")");
                        ifBody.Line("if (fnHandle) fnHandle();");
                    });
                }

                fncBody.Assign("isInitialized", "false");
            });
        }

        private void EmitReleaseFunction(CppInjectorsEmitterContext ctx, CppWriter w)
        {
            var name = ctx.Compilation.Name;
            w.Indent();
            w.Line($"if (isInitialized) Shutdown_{name}();");
        }

        private void EmitSetupFunction(CppInjectorsEmitterContext ctx, CppWriter w)
        {
            var name = ctx.Compilation.Name;
            w.Indent();
            w.Line($"EventSystem_OnGameStart(&Startup_{name});");
            w.Line($"EventSystem_OnGameEnd(&Shutdown_{name});");
            w.Line($"Init_{name}();");
        }
    }
}