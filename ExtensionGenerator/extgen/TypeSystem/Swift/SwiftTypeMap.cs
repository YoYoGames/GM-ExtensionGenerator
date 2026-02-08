using codegencore.Models;

namespace extgen.TypeSystem.Swift
{
    internal sealed class SwiftTypeMap : IIrTypeMap
    {
        public string Map(IrType t, bool owned = false)
        {
            // 1) Nullable wrapper applies last
            var isNullable = IrType.IsNullable(t);
            t = IrType.StripNullable(t);

            // 2) Map non-nullable
            var core = MapNonNullable(t, owned);

            // 3) Swift optional
            if (isNullable)
                core += "?";

            return core;
        }

        private string MapNonNullable(IrType t, bool owned)
        {
            return t switch
            {
                IrType.Array a => MapArray(a, owned),

                IrType.Named n => n.Kind switch
                {
                    NamedKind.Enum => MapEnum(n),
                    NamedKind.Struct => MapStruct(n),
                    _ => n.Name
                },

                IrType.Builtin b => MapBuiltin(b, owned),

                _ => "Any"
            };
        }

        private string MapArray(IrType.Array a, bool owned)
        {
            // Swift: always [T]
            // Element mapping is recursive (so optional elements become T?)
            var elem = Map(a.Element, owned);
            return $"[{elem}]";
        }

        private string MapBuiltin(IrType.Builtin b, bool owned)
        {
            return b.Kind switch
            {
                BuiltinKind.Void => "Void",

                BuiltinKind.Bool => "Bool",

                BuiltinKind.Int8 => "Int8",
                BuiltinKind.Int16 => "Int16",
                BuiltinKind.Int32 => "Int32",
                BuiltinKind.Int64 => "Int64",

                BuiltinKind.UInt8 => "UInt8",
                BuiltinKind.UInt16 => "UInt16",
                BuiltinKind.UInt32 => "UInt32",
                BuiltinKind.UInt64 => "UInt64",

                BuiltinKind.Float32 => "Float",
                BuiltinKind.Float64 => "Double",

                BuiltinKind.String => "String",

                BuiltinKind.Any => "GMValue",
                BuiltinKind.AnyArray => "[GMValue]",
                BuiltinKind.AnyMap => "[(String, GMValue)]",

                // Preserve your previous rule: buffer cannot be returned/owned
                BuiltinKind.Buffer => owned
                    ? throw new NotSupportedException("code emitter: buffer as return is not supported.")
                    : "GMBuffer",

                BuiltinKind.Function => "GMFunction",

                _ => "Any"
            };
        }

        private static string MapEnum(IrType.Named n) => n.Name;

        private static string MapStruct(IrType.Named n) => n.Name;

        public string MapPassType(IrType t, bool owned = false) => Map(t, owned);
    }
}
