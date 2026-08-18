using codegencore.Models;
using extgen.Models.Utils;

namespace extgen.Emitters.Rust
{
    /// <summary>
    /// Maps IR types to Rust type names (owned vs borrowed for strings / Any / handles).
    /// </summary>
    internal sealed class RustTypeMap(IIrTypeEnumResolver enums)
    {
        private readonly IIrTypeEnumResolver _enums = enums;

        public string Map(IrType t, bool owned = true)
        {
            var isNullable = IrType.IsNullable(t);
            t = IrType.StripNullable(t);
            var core = MapNonNullable(t, owned);
            return isNullable ? $"Option<{core}>" : core;
        }

        /// <summary>Return / owned stream types.</summary>
        public string MapOwned(IrType t) => Map(t, owned: true);

        /// <summary>
        /// User-stub parameter types: owned scalars/structs, handle/Any snapshot forms
        /// (Function/Buffer/Any* use <c>owned: false</c> mapping).
        /// </summary>
        public string MapParam(IrType t)
        {
            var isNullable = IrType.IsNullable(t);
            var coreT = IrType.StripNullable(t);
            // Buffer-mode decode materializes String; keep owned String for params.
            if (coreT is IrType.Builtin { Kind: BuiltinKind.String })
                return isNullable ? "Option<String>" : "String";

            var useOwned = coreT switch
            {
                IrType.Builtin { Kind: BuiltinKind.Any or BuiltinKind.AnyArray or BuiltinKind.AnyMap
                    or BuiltinKind.Function or BuiltinKind.Buffer } => false,
                _ => true
            };
            return Map(t, owned: useOwned);
        }

        private string MapNonNullable(IrType t, bool owned) =>
            t switch
            {
                IrType.Array a => MapArray(a, owned),
                IrType.Named n => n.Kind switch
                {
                    NamedKind.Enum => $"enums::{RustCodeGen.SanitizeIdent(n.Name)}",
                    NamedKind.Struct => $"structs::{RustCodeGen.SanitizeIdent(n.Name)}",
                    _ => RustCodeGen.SanitizeIdent(n.Name)
                },
                IrType.Builtin b => MapBuiltin(b, owned),
                _ => throw new NotSupportedException($"rust emitter: unsupported IrType '{t.GetType().Name}'.")
            };

        private string MapArray(IrType.Array a, bool owned)
        {
            var elem = Map(a.Element, owned);
            return a.FixedLength is int n
                ? $"[{elem}; {n}]"
                : $"Vec<{elem}>";
        }

        private static string MapBuiltin(IrType.Builtin b, bool owned) =>
            b.Kind switch
            {
                BuiltinKind.Void => "()",
                BuiltinKind.Bool => "bool",
                BuiltinKind.Int8 => "i8",
                BuiltinKind.Int16 => "i16",
                BuiltinKind.Int32 => "i32",
                BuiltinKind.Int64 => "i64",
                BuiltinKind.UInt8 => "u8",
                BuiltinKind.UInt16 => "u16",
                BuiltinKind.UInt32 => "u32",
                BuiltinKind.UInt64 => "u64",
                BuiltinKind.Float32 => "f32",
                BuiltinKind.Float64 => "f64",
                BuiltinKind.String => owned ? "String" : "&str",
                BuiltinKind.Pointer => "*mut u8",
                BuiltinKind.Buffer => owned
                    ? throw new NotSupportedException(
                        "rust emitter: buffer as owning/return type is not supported.")
                    : "gm_ext_wire::GMBuffer",
                BuiltinKind.Function => owned
                    ? throw new NotSupportedException(
                        "rust emitter: function as owning/return type is not supported.")
                    : "gm_ext_wire::GMFunction",
                BuiltinKind.Any => owned
                    ? "gm_ext_wire::DataStream"
                    : "gm_ext_wire::GMValueOwned",
                BuiltinKind.AnyArray => owned
                    ? "gm_ext_wire::ArrayStream"
                    : "Vec<gm_ext_wire::GMValueOwned>",
                BuiltinKind.AnyMap => owned
                    ? "gm_ext_wire::StructStream"
                    : "std::collections::HashMap<String, gm_ext_wire::GMValueOwned>",
                _ => throw new NotSupportedException($"rust emitter: builtin '{b.Kind}' is not supported.")
            };

        public IrType EnumUnderlying(string enumName) => _enums.GetUnderlying(enumName);
    }
}
