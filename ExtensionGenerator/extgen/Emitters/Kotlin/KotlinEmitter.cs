using codegencore.Writers;
using codegencore.Writers.Lang;
using extgen.Bridge.Kotlin;
using extgen.Emitters.Java;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem.Java;
using extgen.TypeSystem.Kotlin;
using extgen.Utils;
using System.Collections.Immutable;
using System.Text;

namespace extgen.Emitters.Kotlin
{
    internal sealed class KotlinEmitter(JavaEmitterOptions options, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly KotlinTypeMap typeMap = new();

        public void Emit(IrCompilation comp, string dir)
        {
            var ctx = new KotlinEmitterContext(comp.Name, options, runtime);
            var layout = new JavaLayout(dir, options);
            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(KotlinEmitterContext ctx, IrCompilation c, JavaLayout layout)
        {
            // Emit the common Java layer
            EmitJavaLayer(ctx, c, layout);

            // Kotlin shares artifacts but emits .kt instead of .java
            FileEmitHelpers.WriteKotlin(layout.BaseDir, $"{c.Name}Interface.kt", w => EmitKotlinInterface(ctx, c.Functions, w));

            FileEmitHelpers.WriteKotlinIfMissing(layout.BaseDir, $"{c.Name}Kotlin.kt", w => EmitKotlinImpl(ctx, w));
        }

        // ------------- artifacts & entry points (Java)
        private static void EmitJavaLayer(KotlinEmitterContext ctx, IrCompilation c, JavaLayout layout)
        {
            var javaTypeMap = new JavaTypeMap();
            var javaCtx = new JavaEmitterContext(ctx.ExtName, ctx.Options, ctx.Runtime);

            var wireHelpers = new JavaWireHelpers(javaCtx.Runtime, javaTypeMap);
            var bridge = new KotlinBridge(javaTypeMap, javaCtx.Runtime, wireHelpers);

            var common = new JavaCommonEmitter(javaCtx, javaTypeMap, bridge);
            common.EmitJavaArtifacts(c, layout);
            common.EmitJavaInterface(c, layout);
            common.EmitInternal(c, layout);
            common.EmitJavaUserShell(c, layout);
        }

        // ------------- interface (Kotlin)
        private void EmitKotlinInterface(KotlinEmitterContext ctx, ImmutableArray<IrFunction> funcs, KotlinWriter w)
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
                foreach (var fn in funcs)
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
