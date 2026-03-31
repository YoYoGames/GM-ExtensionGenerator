using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Models;
using extgen.Models.Utils;
using extgen.TypeSystem.Cpp;
using System.Collections.Immutable;

namespace extgen.Emitters.Cpp
{
    /// <summary>
    /// Emits common C++ artifacts including constants, enums, structs, codecs, and struct traits.
    /// </summary>
    internal class CppCommonEmitter<T>(CppEmitterContext ctx, CppTypeMap typeMap, IIrTypeEnumResolver enums) where T : CxxWriter<T>
    {
        private readonly CppWireHelpers<T> cppWireHelpers = new CppWireHelpers<T>(ctx.Runtime, typeMap, enums, true);

        /// <summary>
        /// Emits common C++ includes required for generated code.
        /// </summary>
        public static void EmitCommonIncludes(T w) =>
            w.Include("cstdint", true)
             .Include("string_view", true)
             .Include("vector", true)
             .Include("array", true)
             .Include("optional", true);

        /// <summary>
        /// Emits all C++ artifacts for a compilation.
        /// </summary>
        public void EmitCommonCppArtifacts(T w, IrCompilation c)
        {
            EmitConstants(w, c.Constants);
            EmitEnums(w, c.Enums);
            EmitStructs(w, c.Structs);
            EmitCodecs(w, c.Structs);
            EmitStructTraits(w, c.Structs);
        }

        private void EmitConstants(T w, ImmutableArray<IrConstant> constants)
        {
            var constsNs = ctx.Runtime.ConstantsNamespace;
            w.Namespace(constsNs, ns =>
            {
                foreach (var c in constants)
                {
                    ns.DeclareEq(
                        typeMap.Map(c.Type, owned: false),
                        c.Name,
                        c.Literal,
                        modifiers: ["inline", "constexpr"]);
                }
            });
            w.Line();
        }

        private void EmitEnums(T w, IImmutableList<IrEnum> enums)
        {
            var enumsNs = ctx.Runtime.EnumsNamespace;
            w.Namespace(enumsNs, ns =>
            {
                foreach (var e in enums)
                {
                    ns.Enum(
                        e.Name,
                        e.Members.Select(m => new EnumMember(m.Name, m.DefaultLiteral)),
                        underlying: typeMap.Map(e.Underlying, owned: false))
                      .Line();
                }
            });
            w.Line();
        }

        private void EmitStructs(T w, IImmutableList<IrStruct> structs)
        {
            var structsNs = ctx.Runtime.StructsNamespace;
            w.Namespace(structsNs, ns =>
            {
                foreach (var s in structs) ns.Line($"struct {s.Name};");
                ns.Line();

                foreach (var s in structs)
                {
                    ns.Struct(s.Name, body =>
                    {
                        body.ForEach(s.Fields, (stmt, f) =>
                            stmt.Declare(typeMap.Map(f.Type, owned: true), f.Name));
                    })
                    .Line();
                }
            });
        }

        private void EmitCodecs(T w, IImmutableList<IrStruct> structs)
        {
            var codegenNs = ctx.Runtime.CodeGenNamespace;
            var structsNs = ctx.Runtime.StructsNamespace;
            var byteioNS = ctx.Runtime.ByteIONamespace;

            w.Namespace(codegenNs, ns =>
            {
                List<Param> writerParams = [new($"{byteioNS}::IByteWriter&", "_buf")];
                List<Param> readerParams = [new($"{byteioNS}::BufferReader&", "_buf")];

                foreach (var s in structs)
                {
                    var fq = $"{structsNs}::{s.Name}";

                    ns.Line("template<>")
                      .Function($"writeValue<{fq}>",
                          writerParams.Append(new($"const {fq}&", "obj")),
                          body => body.ForEach(s.Fields, (expr, f) =>
                              cppWireHelpers.EncodeLines(expr, f.Type, $"obj.{f.Name}", "_buf")),
                          modifiers: ["inline"])
                      .Line();

                    ns.Line("template<>")
                      .Function($"readValue<{fq}>",
                          readerParams,
                          body =>
                          {
                              body.DeclareBraces(fq, "obj", string.Empty);
                              body.ForEach(s.Fields, (expr, f) =>
                                  cppWireHelpers.DecodeLines(expr, f.Type, $"obj.{f.Name}", declare: false, "_buf", owned: true));
                              body.Return("obj");
                          },
                          returnType: fq,
                          modifiers: ["inline"])
                      .Line();
                }
            });
        }

        private void EmitStructTraits(T w, ImmutableArray<IrStruct> structs)
        {
            w.Namespace(ctx.Runtime.ExtWireDetailsNamespace, nsBody =>
            {
                foreach (var (s, i) in structs.Select((s, i) => (s, i)))
                {
                    nsBody.Line("template<>");
                    nsBody.Struct($"gm_struct_traits<{ctx.Runtime.StructsNamespace}::{s.Name}>", structBody =>
                    {
                        structBody.DeclareEq("bool", "is_gm_struct", "true", modifiers: ["static", "constexpr"]);
                        structBody.DeclareEq("std::uint32_t", "codec_id", $"{i}", modifiers: ["static", "constexpr"]);
                    });
                    nsBody.Line();
                }
            });
        }

        /// <summary>
        /// Emits argument decoding code and returns the list of call arguments.
        /// </summary>
        public List<string> EmitDecode(T w, IrFunction fn, bool needArgs, string br)
        {
            var callArgs = new List<string>();

            if (needArgs)
            {
                w.DeclareBraces(
                    $"{ctx.Runtime.ByteIONamespace}::BufferReader",
                    br,
                    $"{ctx.Runtime.ArgBufferParam}, static_cast<size_t>({ctx.Runtime.ArgBufferLengthParam})");
                w.Line();

                foreach (var p in fn.Parameters)
                {
                    w.Comment($"field: {p.Name}, type: {ToDebugString(p.Type)}");
                    cppWireHelpers.DecodeLines(w, p.Type, p.Name, declare: true, br, owned: false);
                    w.Line();
                    callArgs.Add(p.Name);
                }
                return callArgs;
            }

            foreach (var p in IrAnalysis.DirectArgs(fn))
            {
                if (IsNumericScalar(p.Type))
                    callArgs.Add($"static_cast<{typeMap.Map(p.Type, owned: false)}>({p.Name})");
                else
                    callArgs.Add(p.Name);
            }

            return callArgs;
        }

        /// <summary>
        /// Emits return encoding code or direct return to GML.
        /// </summary>
        public void EmitEncodeReturn(T w, IrType ret, string result, bool needRet, string bw)
        {
            if (IsVoid(ret))
            {
                w.Line("return 0;");
                return;
            }

            if (needRet)
            {
                w.DeclareBraces(
                    $"{ctx.Runtime.ByteIONamespace}::BufferWriter",
                    bw,
                    $"{ctx.Runtime.RetBufferParam}, static_cast<size_t>({ctx.Runtime.RetBufferLengthParam})");
                w.Line();

                w.Comment($"return: {result}, type: {ToDebugString(ret)}");
                cppWireHelpers.EncodeLines(w, ret, result, bw);
                w.Line("return 0;");
                return;
            }

            if (ContainsNullable(ret))
            {
                w.Line("return 0;");
                return;
            }

            if (IsNumericScalar(ret))
            {
                w.Line($"return static_cast<double>({result});");
                return;
            }

            if (IsStringScalar(ret))
            {
                w.Line($"return (char*){result}.c_str();");
                return;
            }

            w.Line("return 0;");
        }

        private static bool IsVoid(IrType t) =>
            t is IrType.Builtin { Kind: BuiltinKind.Void };

        private static bool ContainsNullable(IrType t) =>
            t switch
            {
                IrType.Nullable => true,
                IrType.Array a => ContainsNullable(a.Element),
                _ => false
            };

        private static bool IsNumericScalar(IrType t)
        {
            return t is IrType.Builtin
            {
                Kind: BuiltinKind.Bool
                or BuiltinKind.Int8 or BuiltinKind.UInt8
                or BuiltinKind.Int16 or BuiltinKind.UInt16
                or BuiltinKind.Int32 or BuiltinKind.UInt32
                or BuiltinKind.Int64 or BuiltinKind.UInt64
                or BuiltinKind.Float32 or BuiltinKind.Float64
            };
        }

        private static bool IsStringScalar(IrType t) =>
            t is IrType.Builtin { Kind: BuiltinKind.String };

        private static string ToDebugString(IrType t) =>
            t switch
            {
                IrType.Nullable n => $"optional<{ToDebugString(n.Underlying)}>",
                IrType.Array a => a.FixedLength is int n
                    ? $"{ToDebugString(a.Element)}[{n}]"
                    : $"{ToDebugString(a.Element)}[]",

                IrType.Named { Kind: NamedKind.Struct, Name: var s } => $"struct {s}",
                IrType.Named { Kind: NamedKind.Enum, Name: var e } => $"enum {e}",

                IrType.Builtin b => b.Kind.ToString(),
                _ => t.ToString() ?? "type"
            };
    }
}
