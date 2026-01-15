using codegencore.Writers;
using codegencore.Writers.Concrete;
using extgen.Model;
using extgen.Options;
using System.Collections.Immutable;
using System.Text;

namespace extgen.Emitters.Doc
{
    internal class DocEmitter(DocEmitterOptions options, RuntimeNaming runtime) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            var ext = comp.Name;
            var layout = new DocLayout(outputDir, options);

            WriteFileDoc(layout.OutputDir, $"documentation.js", w => EmitAll(w, comp));
        }
        private static void WriteFileDoc(string dir, string name, Action<DocWriter> emit) 
        {
            Directory.CreateDirectory(dir);
            using var tw = new StreamWriter(Path.Combine(dir, name), false, new UTF8Encoding(false));
            var text = new TextCodeWriter(tw, "    ");
            var doc = new DocWriter(text);
            emit(doc);
        }

        private static void EmitAll(DocWriter w, IrCompilation c)
        {
            // Functions
            EmitFunctions(w, c.Functions);

            // Structs
            EmitStructs(w, c.Structs);

            // Enums
            EmitEnums(w, c.Enums);

            // Constants
            EmitConstants(w, c.Constants);
        }

        private static void EmitFunctions(DocWriter w, ImmutableArray<IrFunction> functions)
        {
            foreach (var f in functions)
            {
                w.JsDoc(spec =>
                {
                    spec.Tag("function_partial", f.Name);
                    foreach (var p in f.Parameters)
                    {
                        spec.Param(new(p.Name, JsDocType(p.Type), null, p.Type.IsNullable));
                    }
                    if (f.ReturnType.Kind != IrTypeKind.Void) spec.Returns(JsDocType(f.ReturnType));
                    spec.Tag("function_end");
                });
                w.Line();
            }
        }

        private static void EmitStructs(DocWriter w, IImmutableList<IrStruct> structs)
        {
            foreach (var s in structs)
            {
                w.JsDoc(spec =>
                {
                    spec.Tag("struct_partial", s.Name);
                    foreach (var f in s.Fields)
                    {
                        spec.Member(new(f.Name, JsDocType(f.Type), null, f.Type.IsNullable));
                    }
                    spec.Tag("struct_end");
                });
                w.Line();
            }
        }

        private static void EmitEnums(DocWriter w, IImmutableList<IrEnum> enums)
        {
            foreach (var e in enums)
            {
                w.JsDoc(spec =>
                {
                    spec.Tag("enum_partial", e.Name);
                    foreach (var m in e.Members)
                    {
                        spec.Member(new(m.Name, JsDocType(e.Underlying)));
                    }
                    spec.Tag("enum_end");
                });
                w.Line();
            }
        }

        private static void EmitConstants(DocWriter w, ImmutableArray<IrConstant> constants)
        {
        }

        public static string JsDocType(IrType t)
        {
            if (t.IsCollection)
            {
                var inner = t with { IsCollection = false };
                return $"Array[{JsDocType(inner)}]";
            }

            return t.Kind switch
            {
                IrTypeKind.Scalar when t.Name == "bool" => "Bool",
                IrTypeKind.Scalar when t.IsNumericScalar => "Real",
                IrTypeKind.Scalar when t.IsStringScalar => "String",
                IrTypeKind.Scalar => "Real",
                IrTypeKind.AnyArray => "Array",
                IrTypeKind.AnyMap => "Struct",
                IrTypeKind.Function => $"Function",
                IrTypeKind.Buffer => $"Id.Buffer",
                IrTypeKind.Struct => $"Struct.{t.Name}",
                IrTypeKind.Enum => $"Enum.{t.Name}",
                IrTypeKind.Variant => "Any",
                IrTypeKind.Void => throw new NotImplementedException(),
                _ => throw new NotImplementedException($"JSDoc conversion not implemented for type: {t}"),
            };
        }
    }
}
