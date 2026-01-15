using codegencore.Writers.Lang;
using extgen.Model;
using extgen.TypeSystem.Cpp;
using System.Collections.Immutable;

namespace extgen.Emitters.Cpp
{
    internal class CppCommonEmitter<T>(CppEmitterContext ctx, CppTypeMap typeMap) where T : CxxWriter<T>
    {
        private readonly CppWireHelpers<T> wireHelpers = new(ctx.Runtime, typeMap);

        public static void EmitCommonIncludes(T w) =>
            w.Include("cstdint", true)
            .Include("string_view", true)
            .Include("vector", true)
            .Include("array", true)
            .Include("optional", true);

        public void EmitCommonCppArtifacts(T w, IrCompilation c)
        {
            // Constants
            EmitConstants(w, c.Constants);

            // Enums
            EmitEnums(w, c.Enums);

            // Structs
            EmitStructs(w, c.Structs);
            
            // Codecs
            EmitCodecs(w, c.Structs);

            // Struct Trails
            EmitStructTraits(w, c.Structs);
        }

        private void EmitConstants(T w, ImmutableArray<IrConstant> constants)
        {
            var constsNs = ctx.Runtime.ConstantsNamespace;
            w.Namespace(constsNs, ns =>
            {
                foreach (var c in constants)
                    w.DeclareEq(typeMap.Map(c.Type, false), c.Name, c.Literal, ["inline", "constexpr"]);
            });
            w.Line();
        }

        private void EmitEnums(T w, IImmutableList<IrEnum> enums)
        {
            var enumsNs = ctx.Runtime.EnumsNamespace;
            w.Namespace(enumsNs, ns =>
            {
                foreach (var e in enums)
                    ns.Enum(e.Name, e.Members.Select(m => new EnumMember(m.Name, m.DefaultLiteral)), typeMap.Map(e.Underlying)).Line();
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
                    ns.Struct(s.Name, structBody => 
                        structBody.ForEach(s.Fields, (declStatement, f) => 
                            declStatement.Declare(typeMap.Map(f.Type, true), f.Name)))
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
                    ns
                    .Line("template<>")
                    .Function($"writeValue<{fq}>", writerParams.Append(new($"const {fq}&", "obj")), body =>
                        body.ForEach(s.Fields, (expr, f) => wireHelpers.EncodeLines(expr, f.Type, $"obj.{f.Name}", "_buf"))
                    , modifiers: ["inline"])
                    .Line();

                    ns
                    .Line("template<>")
                    .Function($"readValue<{fq}>", readerParams, body =>
                        body.DeclareBraces(fq, "obj", string.Empty)
                        .ForEach(s.Fields, (expr, f) => wireHelpers.DecodeLines(expr, f.Type, $"obj.{f.Name}", false, "_buf", true))
                        .Return("obj")
                    , fq, modifiers: ["inline"])
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

        public List<string> EmitDecode(T w, IrFunction fn, bool needArgs, string br)
        {
            var callArgs = new List<string>();
            if (needArgs)
            {
                w.DeclareBraces($"{ctx.Runtime.ByteIONamespace}::BufferReader", br, $"{ctx.Runtime.ArgBufferParam}, static_cast<size_t>({ctx.Runtime.ArgBufferLengthParam})");
                w.Line();
                foreach (var p in fn.Parameters)
                {
                    w.Comment($"field: {p.Name}, type: {p.Type.Name}{(p.Type.IsCollection ? $"[{p.Type.FixedLength}]" : "")}");
                    wireHelpers.DecodeLines(w, p.Type, p.Name, true, br, false);
                    w.Line();
                    callArgs.Add(p.Name);
                }
            }
            else
            {
                foreach (var p in IrAnalysis.DirectArgs(fn))
                {
                    callArgs.Add(p.Type.IsNumericScalar ? $"static_cast<{typeMap.Map(p.Type)}>({p.Name})" : p.Name);
                }
            }
            return callArgs;
        }

        public void EmitEncodeReturn(T w, IrType ret, string result, bool needRet, string bw)
        {
            if (ret.Kind == IrTypeKind.Void)
            {
                w.Line("return 0;");
                return;
            }
            if (needRet)
            {
                w.DeclareBraces($"{ctx.Runtime.ByteIONamespace}::BufferWriter", bw, $"{ctx.Runtime.RetBufferParam}, static_cast<size_t>({ctx.Runtime.RetBufferLengthParam})");
                w.Line();
                w.Comment($"return: {result}, type: {ret.Name}{(ret.IsCollection ? $"[{ret.FixedLength}]" : "")}");
                wireHelpers.EncodeLines(w, ret, result, bw);
                w.Line("return 0;");
                return;
            }
            // direct
            if (ret.IsNumericScalar) w.Line($"return static_cast<double>({result});");
            else if (ret.IsStringScalar) w.Line($"return (char*){result}.c_str();");
            else w.Line("return 0;");
        }

    }
}
