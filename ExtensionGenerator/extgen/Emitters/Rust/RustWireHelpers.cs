using codegencore.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using System.Text;

namespace extgen.Emitters.Rust
{
    /// <summary>
    /// Emits Rust IDL buffer-protocol encode/decode statements (mirror CppWireHelpers).
    /// </summary>
    internal sealed class RustWireHelpers(
        RustTypeMap typeMap,
        IIrTypeEnumResolver enums,
        RuntimeNaming runtime,
        string structCodecPrefix = "codecs::")
    {
        private readonly RustTypeMap _typeMap = typeMap;
        private readonly IIrTypeEnumResolver _enums = enums;
        private readonly RuntimeNaming _runtime = runtime;
        private readonly string _structCodecPrefix = structCodecPrefix;

        /// <summary>
        /// Emit decode of <paramref name="t"/> from reader into a let-binding <paramref name="accessor"/>.
        /// </summary>
        public void DecodeLet(StringBuilder sb, string indent, IrType t, string accessor, string reader)
        {
            var rustTy = _typeMap.MapParam(t);
            sb.Append(indent).Append("let ").Append(accessor).Append(": ").Append(rustTy)
              .Append(" = ").Append(DecodeExpr(t, reader)).Append(";\n");
        }

        /// <summary>
        /// Emit encode of <paramref name="accessor"/> into writer.
        /// </summary>
        public void EncodeStmt(StringBuilder sb, string indent, IrType t, string accessor, string writer)
        {
            sb.Append(indent).Append(EncodeExpr(t, writer, accessor)).Append(";\n");
        }

        public string DecodeExpr(IrType t, string reader)
        {
            if (t is IrType.Nullable n)
            {
                var inner = n.Underlying;
                return $"{{ let __has = {reader}.read_bool()?; if __has {{ Some({DecodeExpr(inner, reader)}) }} else {{ None }} }}";
            }

            if (t is IrType.Array a)
            {
                var elTy = _typeMap.MapParam(a.Element);
                if (a.FixedLength is int nFixed)
                {
                    return $"{{ let mut __v: Vec<{elTy}> = Vec::with_capacity({nFixed}); for _ in 0..{nFixed} {{ __v.push({DecodeExpr(a.Element, reader)}); }} let __arr: [{elTy}; {nFixed}] = __v.try_into().ok()?; __arr }}";
                }

                return $"{{ let __n = {reader}.read_u32()? as usize; let mut __v = Vec::with_capacity(__n); for _ in 0..__n {{ __v.push({DecodeExpr(a.Element, reader)}); }} __v }}";
            }

            if (t is IrType.Named { Kind: NamedKind.Struct, Name: var structName })
            {
                var id = RustCodeGen.SanitizeIdent(structName);
                return $"{_structCodecPrefix}decode_{id}(&mut {reader})?";
            }

            if (t is IrType.Named { Kind: NamedKind.Enum, Name: var enumName })
            {
                var id = RustCodeGen.SanitizeIdent(enumName);
                var underlying = _enums.GetUnderlying(enumName);
                var read = ScalarRead(underlying, reader);
                return $"enums::{id}::try_from({read}).ok()?";
            }

            if (t is IrType.Builtin b)
                return BuiltinRead(b, reader);

            throw new NotSupportedException($"rust emitter: cannot decode '{t}'.");
        }

        public string EncodeExpr(IrType t, string writer, string accessor)
        {
            if (t is IrType.Nullable n)
            {
                var inner = n.Underlying;
                return $"{{ if let Some(ref __v) = {accessor} {{ {writer}.write_bool(true)?; {EncodeExpr(inner, writer, "(*__v)")}; }} else {{ {writer}.write_bool(false)?; }} }}";
            }

            if (t is IrType.Array a)
            {
                if (a.FixedLength is int)
                {
                    return $"{{ for __el in &{accessor} {{ {EncodeExpr(a.Element, writer, "(*__el)")}; }} }}";
                }

                return $"{{ {writer}.write_u32({accessor}.len() as u32)?; for __el in &{accessor} {{ {EncodeExpr(a.Element, writer, "*__el")}; }} }}";
            }

            if (t is IrType.Named { Kind: NamedKind.Struct, Name: var structName })
            {
                var id = RustCodeGen.SanitizeIdent(structName);
                return $"{_structCodecPrefix}encode_{id}(&mut {writer}, &{accessor})?";
            }

            if (t is IrType.Named { Kind: NamedKind.Enum, Name: var enumName })
            {
                var underlying = _enums.GetUnderlying(enumName);
                var asUnderlying = CastEnumToUnderlying(underlying, accessor);
                return ScalarWrite(underlying, writer, asUnderlying);
            }

            if (t is IrType.Builtin b)
                return BuiltinWrite(b, writer, accessor);

            throw new NotSupportedException($"rust emitter: cannot encode '{t}'.");
        }

        private string BuiltinRead(IrType.Builtin b, string reader) =>
            b.Kind switch
            {
                BuiltinKind.Bool => $"{reader}.read_bool()?",
                BuiltinKind.Int8 => $"{reader}.read_i8()?",
                BuiltinKind.UInt8 => $"{reader}.read_u8()?",
                BuiltinKind.Int16 => $"{reader}.read_i16()?",
                BuiltinKind.UInt16 => $"{reader}.read_u16()?",
                BuiltinKind.Int32 => $"{reader}.read_i32()?",
                BuiltinKind.UInt32 => $"{reader}.read_u32()?",
                BuiltinKind.Int64 => $"{reader}.read_i64()?",
                BuiltinKind.UInt64 => $"{reader}.read_u64()?",
                BuiltinKind.Float32 => $"{reader}.read_f32()?",
                BuiltinKind.Float64 => $"{reader}.read_f64()?",
                BuiltinKind.String => $"{reader}.read_idl_string()?.to_string()",
                BuiltinKind.Pointer => $"{reader}.read_u64()? as *mut u8",
                BuiltinKind.Function =>
                    $"gm_ext_wire::GMFunction::from_u64({reader}.read_u64()?, &{_runtime.DispatchQueueField})",
                BuiltinKind.Buffer =>
                    $"{_runtime.BufferQueueField}.pop_front()?",
                BuiltinKind.Any =>
                    $"{reader}.unpack_value_owned()?",
                BuiltinKind.AnyArray =>
                    $"{{ match {reader}.unpack_value_owned()? {{ gm_ext_wire::GMValueOwned::Array(__a) => __a, _ => return None }} }}",
                BuiltinKind.AnyMap =>
                    $"{{ match {reader}.unpack_value_owned()? {{ gm_ext_wire::GMValueOwned::Struct(__m) => __m, _ => return None }} }}",
                _ => throw new NotSupportedException($"rust emitter: cannot read builtin '{b.Kind}'.")
            };

        private static string BuiltinWrite(IrType.Builtin b, string writer, string accessor) =>
            b.Kind switch
            {
                BuiltinKind.Bool => $"{writer}.write_bool({accessor})?",
                BuiltinKind.Int8 => $"{writer}.write_i8({accessor})?",
                BuiltinKind.UInt8 => $"{writer}.write_u8({accessor})?",
                BuiltinKind.Int16 => $"{writer}.write_i16({accessor})?",
                BuiltinKind.UInt16 => $"{writer}.write_u16({accessor})?",
                BuiltinKind.Int32 => $"{writer}.write_i32({accessor})?",
                BuiltinKind.UInt32 => $"{writer}.write_u32({accessor})?",
                BuiltinKind.Int64 => $"{writer}.write_i64({accessor})?",
                BuiltinKind.UInt64 => $"{writer}.write_u64({accessor})?",
                BuiltinKind.Float32 => $"{writer}.write_f32({accessor})?",
                BuiltinKind.Float64 => $"{writer}.write_f64({accessor})?",
                BuiltinKind.String => $"{writer}.write_idl_string({accessor}.as_str())?",
                BuiltinKind.Pointer => $"{writer}.write_u64({accessor} as u64)?",
                BuiltinKind.Any or BuiltinKind.AnyArray or BuiltinKind.AnyMap =>
                    $"{accessor}.write_to(&mut {writer})?",
                BuiltinKind.Function => $"{writer}.write_idl_function({accessor}.id())?",
                BuiltinKind.Buffer =>
                    $"{writer}.write_idl_buffer({accessor}.len as u32, {accessor}.ptr as u64)?",
                _ => throw new NotSupportedException($"rust emitter: cannot write builtin '{b.Kind}'.")
            };

        private static string ScalarRead(IrType t, string reader)
        {
            if (t is not IrType.Builtin b)
                throw new NotSupportedException($"rust emitter: expected builtin scalar, got '{t}'.");
            return b.Kind switch
            {
                BuiltinKind.Bool => $"{reader}.read_bool()?",
                BuiltinKind.Int8 => $"{reader}.read_i8()?",
                BuiltinKind.UInt8 => $"{reader}.read_u8()?",
                BuiltinKind.Int16 => $"{reader}.read_i16()?",
                BuiltinKind.UInt16 => $"{reader}.read_u16()?",
                BuiltinKind.Int32 => $"{reader}.read_i32()?",
                BuiltinKind.UInt32 => $"{reader}.read_u32()?",
                BuiltinKind.Int64 => $"{reader}.read_i64()?",
                BuiltinKind.UInt64 => $"{reader}.read_u64()?",
                BuiltinKind.Float32 => $"{reader}.read_f32()?",
                BuiltinKind.Float64 => $"{reader}.read_f64()?",
                BuiltinKind.String => $"{reader}.read_idl_string()?.to_string()",
                BuiltinKind.Pointer => $"{reader}.read_u64()? as *mut u8",
                _ => throw new NotSupportedException($"rust emitter: cannot read builtin '{b.Kind}'.")
            };
        }

        private static string ScalarWrite(IrType t, string writer, string accessor)
        {
            if (t is not IrType.Builtin b)
                throw new NotSupportedException($"rust emitter: expected builtin scalar, got '{t}'.");
            return BuiltinWrite(b, writer, accessor);
        }

        private static string CastEnumToUnderlying(IrType underlying, string accessor)
        {
            if (underlying is not IrType.Builtin b)
                return $"{accessor} as i32";

            var rust = b.Kind switch
            {
                BuiltinKind.Int8 => "i8",
                BuiltinKind.UInt8 => "u8",
                BuiltinKind.Int16 => "i16",
                BuiltinKind.UInt16 => "u16",
                BuiltinKind.Int32 => "i32",
                BuiltinKind.UInt32 => "u32",
                BuiltinKind.Int64 => "i64",
                BuiltinKind.UInt64 => "u64",
                _ => "i32"
            };
            return $"{accessor} as {rust}";
        }
    }
}
