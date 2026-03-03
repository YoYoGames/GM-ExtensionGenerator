using codegencore.Models;
using codegencore.Writers;
using codegencore.Writers.Concrete;
using extgen.Models;
using extgen.Models.Config;
using System.Collections.Immutable;
using System.Text;

namespace extgen.Emitters.Doc
{
    internal sealed class DocEmitter(DocEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            var ext = comp.Name;
            var layout = new DocLayout(outputDir, settings);

            if (settings.Overwrite || !File.Exists(layout.FullPath))
            {
                WriteFileDoc(layout.OutputDir, $"{string.Format(layout.OutputFile, ext)}", w => EmitAll(w, comp));
            }
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
            EmitFunctions(w, c);
            EmitStructs(w, c.Structs);
            EmitEnums(w, c.Enums);
            EmitConstants(w, c.Constants);
        }

        private static void EmitFunctions(DocWriter w, IrCompilation c)
        {
            var allFunctions = c.GetAllFunctions(PatchStructMethod);

            foreach (var f in allFunctions)
            {
                w.JsDoc(spec =>
                {
                    spec.Tag("function_partial", f.Name);

                    foreach (var p in f.Parameters)
                    {
                        spec.Param(new(
                            p.Name,
                            JsDocType(p.Type),
                            Description: null,
                            Optional: IsNullable(p.Type)));
                    }

                    if (!IsVoid(f.ReturnType))
                        spec.Returns(JsDocType(f.ReturnType));

                    spec.Tag("function_end");
                });

                w.Line();
            }
        }

        private static IrFunction PatchStructMethod(IrStruct s, IrFunction f)
        {
            return f with { Name = $"{s.Name}::{f.Name}" };
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
                        spec.Member(new(
                            f.Name,
                            JsDocType(f.Type),
                            Description: null,
                            Optional: IsNullable(f.Type)));
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
                    // You used const_partial/const_end before; keeping same tags.
                    spec.Tag("enum_partial", e.Name);

                    foreach (var m in e.Members)
                    {
                        spec.Member(new(
                            m.Name,
                            null,
                            Description: null,
                            Optional: false));
                    }

                    spec.Tag("enum_end");
                });

                w.Line();
            }
        }

        private static void EmitConstants(DocWriter w, ImmutableArray<IrConstant> constants)
        {
            w.JsDoc(spec =>
            {
                // You used const_partial/const_end before; keeping same tags.
                spec.Tag("const_partial", "macros");

                foreach (var c in constants)
                {
                    spec.Member(new(
                        c.Name,
                        JsDocType(c.Type),
                        Description: $"(value: '{c.Literal}')",
                        Optional: false));
                }

                spec.Tag("const_end");
            });

            w.Line();
        }

        // ============================================================
        // IrType helpers (new shape-based IrType)
        // ============================================================

        private static bool IsVoid(IrType t) =>
            t is IrType.Builtin { Kind: BuiltinKind.Void };

        private static bool IsNullable(IrType t) =>
            t is IrType.Nullable;

        private static IrType StripNullable(IrType t) =>
            t is IrType.Nullable n ? n.Underlying : t;

        // ============================================================
        // JSDoc type mapping (GameMaker-facing)
        // ============================================================

        public static string JsDocType(IrType t)
        {
            // Nullable affects "optional" flag; for the type string we describe the underlying.
            t = StripNullable(t);

            // Array<T>
            if (t is IrType.Array a)
            {
                return a.FixedLength is int n
                    ? $"Array[{JsDocType(a.Element)}]/*len:{n}*/"
                    : $"Array[{JsDocType(a.Element)}]";
            }

            // Named types (Struct / Enum)
            if (t is IrType.Named named)
            {
                return named.Kind switch
                {
                    NamedKind.Struct => $"Struct.{named.Name}",
                    NamedKind.Enum => $"Enum.{named.Name}",
                    _ => named.Name
                };
            }

            // Builtins
            if (t is IrType.Builtin b)
            {
                return b.Kind switch
                {
                    BuiltinKind.Bool => "Bool",

                    // numeric -> Real (GML number)
                    BuiltinKind.Int8 or BuiltinKind.UInt8
                    or BuiltinKind.Int16 or BuiltinKind.UInt16
                    or BuiltinKind.Int32 or BuiltinKind.UInt32
                    or BuiltinKind.Int64 or BuiltinKind.UInt64
                    or BuiltinKind.Float32 or BuiltinKind.Float64
                        => "Real",

                    BuiltinKind.String => "String",

                    BuiltinKind.Any => "Any",
                    BuiltinKind.AnyArray => "Array",
                    BuiltinKind.AnyMap => "Struct",

                    BuiltinKind.Function => "Function",
                    BuiltinKind.Buffer => "Buffer",

                    BuiltinKind.Void => "Undefined",

                    _ => "Any"
                };
            }

            // Fallback
            return "Any";
        }
    }
}
