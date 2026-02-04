using codegencore.Model;
using codegencore.Writers.Lang;
using extgen.Bridge.Java;
using extgen.Model;
using extgen.Model.Utils;
using extgen.TypeSystem.Java;
using extgen.Utils;

namespace extgen.Emitters.Java
{
    internal sealed class JavaCommonEmitter(JavaEmitterContext ctx, JavaTypeMap typeMap, JavaBridgeGenerator bridge)
    {
        public void EmitJavaArtifacts(IrCompilation c, JavaLayout layout)
        {
            // Enums
            foreach (var e in c.Enums)
                FileEmitHelpers.WriteJava(layout.Enums, $"{e.Name}.java", w => EmitEnum(ctx, e, w));

            // Records
            foreach (var (s, i) in c.Structs.Select((s, i) => (s, i)))
                FileEmitHelpers.WriteJava(layout.Records, $"{s.Name}.java", w => EmitRecord(ctx, s, i, w));

            // Codecs
            var enums = new IrTypeEnumResolver(c.Enums);
            var wireHelpers = new JavaWireHelpers(ctx.Runtime, typeMap, enums);
            foreach (var s in c.Structs)
                FileEmitHelpers.WriteJava(layout.Codecs, $"{s.Name}Codec.java", w => EmitCodec(ctx, s, w, wireHelpers));
        }

        public void EmitInternal(IrCompilation c, JavaLayout layout)
        {
            FileEmitHelpers.WriteJava(layout.CodeGenDir, $"{c.Name}Internal.java", w => EmitInternalImpl(ctx, c, w));
        }

        public void EmitJavaInterface(IrCompilation c, JavaLayout layout)
        {
            FileEmitHelpers.WriteJava(layout.CodeGenDir, $"{c.Name}Interface.java", w => EmitJavaInterfaceImpl(ctx, c, w));
        }

        public void EmitJavaUserShell(IrCompilation c, JavaLayout layout)
        {
            FileEmitHelpers.WriteJavaIfMissing(layout.BaseDir, $"{c.Name}.java", w => EmitImplementation(ctx, c, w));
        }

        // ------------- enums / records / codecs (unchanged, but using helpers)
        private void EmitEnum(JavaEmitterContext ctx, IrEnum e, JavaWriter w)
        {
            string pkg = ctx.Runtime.BasePackage;
            string u = JavaWireHelpers.ScalarForEnum(e.Underlying);

            w.Package($"{pkg}.enums").Line();

            // Build JavaEnumMembers with the underlying numeric literal as ctor argument
            var members = e.Members.Select(m => new JavaEnumMember(m.Name, m.DefaultLiteral, null, u));

            w.Enum(name: e.Name, members: members, body: body =>
            {
                body.Line($"private final {u} value;");

                body.Constructor(
                    e.Name,
                    [new Param(u, "v")],
                    ctor => ctor.Line("this.value = v;"),
                    modifiers: ["private"]);

                body.Function(
                    "value",
                    [],
                    get => get.Return("this.value"),
                    returnType: u,
                    modifiers: ["public"]);

                body.Function(
                    "from",
                    [new Param(u, "v")],
                    from =>
                    {
                        from.Switch("v", build => {
                            foreach (var m in e.Members.Where(m => m.DefaultLiteral is not null))
                                build.Case(m.DefaultLiteral!, caseBody => caseBody.Return($"{e.Name}.{m.Name}"), false);
                            build.Default(defaultBody => defaultBody.Line($"throw new IllegalArgumentException(\"Unknown {e.Name} value: \" + v);"), false);
                        });
                    },
                    returnType: e.Name,
                    modifiers: ["public", "static"]);
            },
            modifiers: ["public"]);
        }

        private void EmitRecord(JavaEmitterContext ctx, IrStruct s, int index, JavaWriter w)
        {
            string pkg = ctx.Runtime.BasePackage;

            w.Package($"{pkg}.records").Line();

            bool needsOptional = s.Fields.Any(f => IrType.IsNullable(f.Type));
            bool needsList = s.Fields.Any(f => f.Type.IsVarArray());
            bool needsArray = s.Fields.Any(f => f.Type.IsFixedArray());
            bool needsEnum = s.Fields.Any(f => f.Type.IsEnum());

            w.Import($"{pkg}.{ctx.Runtime.WireClass}");
            w.Import($"{pkg}.codecs.*");
            if (needsEnum) w.Import($"{pkg}.enums.*");
            w.Line();

            w.Import($"java.nio.ByteBuffer");
            if (needsOptional) w.Import("java.util.Optional");
            if (needsList) w.Import("java.util.List");
            if (needsArray) w.Import("java.util.Arrays");
            w.Line();

            var components = s.Fields.Select(f => new Param(typeMap.Map(f.Type), f.Name));

            w.Record(s.Name, components, recordBody => 
            {
                recordBody.Field("int", "CODEC_ID", $"{index}", ["public", "static", "final"]);

                recordBody.Annotations(["Override"]);
                recordBody.Function("encode", [new("ByteBuffer", "b")], encodeBody => 
                {
                    encodeBody.Call($"{s.Name}Codec.write", "b", "this").Line(";");
                }, "void", modifiers: ["public"]);

            }, implements: [$"{ctx.Runtime.WireClass}.ITypedStruct"], modifiers: ["public"]);
        }

        private void EmitCodec(JavaEmitterContext ctx, IrStruct s, JavaWriter w, JavaWireHelpers wireHelpers)
        {
            string pkg = ctx.Runtime.BasePackage;
            string wire = ctx.Runtime.WireClass;

            w.Package($"{pkg}.codecs").Line();

            bool needsOptional = s.Fields.Any(f => IrType.IsNullable(f.Type));
            bool needsList = s.Fields.Any(f => f.Type.IsVarArray());
            bool needsArray = s.Fields.Any(f => f.Type.IsFixedArray());
            bool needsEnum = s.Fields.Any(f => f.Type.IsEnum());

            w.Import("java.nio.ByteBuffer").Line();

            w.Import($"{pkg}.GMExtWire");
            if (needsOptional) w.Import("java.util.Optional");
            if (needsList) w.Import("java.util.List");
            if (needsArray) w.Import("java.util.Arrays");
            if (needsEnum) w.Import($"{pkg}.enums.*");
            w.Import($"{pkg}.records.*").Line();

            w.Class($"{s.Name}Codec", cls =>
            {
                cls.Constructor($"{s.Name}Codec", [], _ => { }, modifiers: ["private"]);

                cls.Function("read", [new Param("ByteBuffer", "b")], read =>
                {
                    var args = new List<string>();
                    foreach (var f in s.Fields)
                    {
                        wireHelpers.DecodeLines(read, f.Type, f.Name, true, "b");
                        args.Add(f.Name);
                        read.Line();
                    }
                    read.Line($"return new {s.Name}({string.Join(", ", args)});");
                }, s.Name, modifiers: ["public", "static"]).Line();

                cls.Function("write", [new Param("ByteBuffer", "b"), new Param(s.Name, "obj")], write =>
                {
                    foreach (var f in s.Fields)
                    {
                        var acc = $"obj.{f.Name}()";
                        wireHelpers.EncodeLines(write, f.Type, acc, "b");
                        write.Line();
                    }
                }, null, modifiers: ["public", "static"]);
            }, ["public", "final"]);
        }

        // ------------- interface (Java)
        private void EmitJavaInterfaceImpl(JavaEmitterContext ctx, IrCompilation c, JavaWriter w)
        {
            string pkg = ctx.Runtime.BasePackage;
            string wire = ctx.Runtime.WireClass;
            string cls = $"{c.Name}Interface";

            bool needsOptional = 
                c.Structs.Any(s => s.Fields.Any(f => f.Type.IsNullable())) || 
                c.Functions.Any(f => f.Parameters.Any(p => p.IsOptional) || f.ReturnType.IsNullable());
            bool needsList =
                c.Structs.Any(s => s.Fields.Any(f => f.Type.IsVarArray())) ||
                c.Functions.Any(f => f.Parameters.Any(p => p.Type.IsVarArray()) ||
                f.ReturnType.IsVarArray());
            bool needsArray =
                c.Structs.Any(s => s.Fields.Any(f => f.Type.IsFixedArray())) ||
                c.Functions.Any(f => f.Parameters.Any(p => p.Type.IsFixedArray()) ||
                f.ReturnType.IsFixedArray());

            bool needsEnum = c.Enums.Any();
            bool needsRecords = c.Structs.Any();

            w.Package(pkg)
                .Import($"{pkg}.{wire}.GMFunction")
                .Import($"{pkg}.{wire}.GMValue");
            if (needsEnum) w.Import($"{pkg}.enums.*");
            if (needsRecords) w.Import($"{pkg}.records.*");
            w.Line();

            if (needsOptional) w.Import("java.util.Optional");
            if (needsList) w.Import("java.util.List");
            if (needsArray) w.Import("java.util.Arrays");

            if (needsOptional || needsList || needsArray) w.Line();

            w.Interface(cls, body =>
            {
                foreach (var fn in c.Functions)
                {
                    var ret = typeMap.Map(fn.ReturnType, owned: true);
                    var ps = fn.Parameters.Select(p => new Param(typeMap.Map(p.Type), p.Name));
                    body.FunctionDecl(fn.Name, ps, ret, modifiers: ["public"]);
                }
            }, ["public"]);
        }

        // ------------- internal bridge (shared – switchable call target)
        private void EmitInternalImpl(JavaEmitterContext ctx, IrCompilation c, JavaWriter w)
        {
            string ext = ctx.ExtName;
            string pkg = ctx.Runtime.BasePackage;
            string wire = ctx.Runtime.WireClass;  // e.g. GMExtWire
            string cls = $"{ext}Internal";

            w.Package(pkg).Line()
             .Import("java.nio.ByteBuffer")
             .Import("java.util.*")
             .Import($"{pkg}.{wire}")
             .Import($"{pkg}.{wire}.GMFunction")
             .Import($"{pkg}.{wire}.GMValue");

            bool needsEnum = c.Enums.Any();
            bool needsRecords = c.Structs.Any();

            if (needsEnum) w.Import($"{pkg}.enums.*");
            if (needsRecords) w.Import($"{pkg}.records.*");

            w.Line();

            // Ask the bridge what this class should implement
            var implements = bridge.GetClassImplements(ctx);

            w.Class(cls, "RunnerSocial", clsBody =>
            {
                clsBody.Line();

                // flavor-specific bits handled by the bridge
                bridge.EmitBackingField(ctx, clsBody);
                bridge.EmitInvocationHandler(ctx, c.Functions, clsBody);
                bridge.EmitBufferQueueHandler(ctx, c.Functions, clsBody);

                foreach (var fn in c.Functions)
                    bridge.EmitFunctionBridge(ctx, fn, clsBody);

                foreach (var cn in c.Constants)
                    w.Const(typeMap.Map(cn.Type, false), cn.Name, cn.Literal, extraModifiers: ["public"]);

            },
            modifiers: ["public", "abstract"],
            implements: implements);
        }

        // ------------- public user shell
        private void EmitImplementation(JavaEmitterContext ctx, IrCompilation c, JavaWriter w)
        {
            string pkg = ctx.Runtime.BasePackage;
            w.Package(pkg).Line();
            w.Class($"{ctx.ExtName}", $"{ctx.ExtName}Internal", _ => { }, ["public"], null);
        }
    }
}
