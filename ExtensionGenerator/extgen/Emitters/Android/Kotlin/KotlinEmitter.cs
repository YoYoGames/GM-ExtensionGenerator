using codegencore.Writers.Lang;
using extgen.Bridge.Kotlin;
using extgen.Emitters.Android.Java;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.Options.Android;
using extgen.TypeSystem.Java;
using extgen.TypeSystem.Kotlin;
using extgen.Utils;
using System.Collections.Immutable;


namespace extgen.Emitters.Android.Kotlin
{
    internal sealed class KotlinEmitter(AndroidEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly KotlinTypeMap typeMap = new();

        public void Emit(IrCompilation comp, string dir)
        {
            var ctx = new KotlinEmitterContext(comp.Name, settings, runtime);
            var layout = new JavaLayout(dir, settings);
            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(KotlinEmitterContext ctx, IrCompilation c, JavaLayout layout)
        {
            // Emit the common Java layer
            EmitJavaLayer(ctx, c, layout);

            // Kotlin shares artifacts but emits .kt instead of .java
            FileEmitHelpers.WriteKotlin(layout.BaseDir, $"{c.Name}Interface.kt", w => EmitKotlinInterface(ctx, c, w));

            FileEmitHelpers.WriteKotlinIfMissing(layout.BaseDir, $"{c.Name}Kotlin.kt", w => EmitKotlinImpl(ctx, w));
        }

        // ------------- artifacts & entry points (Java)
        private static void EmitJavaLayer(KotlinEmitterContext ctx, IrCompilation c, JavaLayout layout)
        {
            var javaTypeMap = new JavaTypeMap();
            var javaCtx = new JavaEmitterContext(ctx.ExtName, ctx.Settings, ctx.Runtime);

            var enums = new IrTypeEnumResolver(c.Enums);
            var wireHelpers = new JavaWireHelpers(javaCtx.Runtime, javaTypeMap, enums);
            var bridge = new KotlinBridge(javaTypeMap, javaCtx.Runtime, wireHelpers);

            var common = new JavaCommonEmitter(javaCtx, javaTypeMap, bridge);
            common.EmitJavaArtifacts(c, layout);
            common.EmitJavaInterface(c, layout);
            common.EmitInternal(c, layout);
            common.EmitJavaUserShell(c, layout);
        }

        // ------------- interface (Kotlin)
        private void EmitKotlinInterface(KotlinEmitterContext ctx, IrCompilation c, KotlinWriter w)
        {
            string pkg = ctx.Runtime.BasePackage;
            string wire = ctx.Runtime.WireClass;

            w.Package(pkg);
            w.Import($"{pkg}.{wire}.GMFunction");
            w.Import($"{pkg}.{wire}.GMValue");
            w.Import($"{pkg}.records.*");
            w.Import($"{pkg}.codecs.*");
            w.Import($"{pkg}.enums.*");

            w.Interface($"{ctx.ExtName}Interface", iface =>
            {
                var allFunctions = c.Functions.Select(f => f).Concat(c.Structs.SelectMany(s => s.Functions.Select(f => IrFunctionUtil.PatchStructMethod(s, f))));
                foreach (var fn in allFunctions)
                {
                    string ret = typeMap.Map(fn.ReturnType, owned: true);

                    var parameters = fn.Parameters
                        .Select(p => (p.Name, typeMap.Map(p.Type)));

                    iface.FunDecl(fn.Name, parameters, ret);
                }
            });
        }

        // ------------- implementation shell (Kotlin)
        private void EmitKotlinImpl(KotlinEmitterContext ctx, KotlinWriter w)
        {
            string pkg = ctx.Runtime.BasePackage;
            string wire = runtime.WireClass;

            w.Package(pkg).Line();

            w.Import($"{pkg}.{wire}.GMFunction");
            w.Import($"{pkg}.{wire}.GMValue");
            w.Import($"{pkg}.records.*");
            w.Import($"{pkg}.codecs.*");
            w.Import($"{pkg}.enums.*").Line();

            // class MyExtKotlin : MyExtInterface { }
            w.Class(
                name: $"{ctx.ExtName}Kotlin",
                baseType: null,
                interfaces: [$"{ctx.ExtName}Interface"],
                body: cls =>
                {
                    // Empty implementation stub.
                    // Users fill this in.
                }
            );
        }
    }
}
