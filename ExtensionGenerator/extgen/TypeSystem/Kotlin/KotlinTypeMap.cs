using codegencore.Models;

namespace extgen.TypeSystem.Kotlin
{
    /// <summary>
    /// Maps IrType -> Kotlin types for the public surface (interfaces, etc.).
    /// owned is mostly irrelevant for Kotlin surface (strings are always String, ByteArray is value),
    /// but kept for interface parity.
    /// </summary>
    public sealed class KotlinTypeMap : IIrTypeMap
    {
        public string Map(IrType t, bool owned = false)
        {
            var isNullable = IrType.IsNullable(t);
            t = IrType.StripNullable(t);

            var core = MapNonNullable(t, owned);

            // Kotlin nullable marker
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
            // Kotlin surface:
            // - fixed length => Array<T>
            // - variable => List<T>
            //
            // Element mapping: use Map(...) recursively so nullable elements become T?
            var elem = Map(a.Element, owned);

            return a.FixedLength is int
                ? $"Array<{elem}>"
                : $"List<{elem}>";
        }

        private string MapBuiltin(IrType.Builtin b, bool owned)
        {
            return b.Kind switch
            {
                BuiltinKind.Void => "Unit",

                BuiltinKind.Bool => "Boolean",

                BuiltinKind.Int8 or BuiltinKind.UInt8 => "Byte",
                BuiltinKind.Int16 or BuiltinKind.UInt16 => "Short",

                // Kotlin has UInt/ULong but on JVM they’re inline classes; your old behavior used signed.
                // Keep it pragmatic and stable:
                BuiltinKind.Int32 or BuiltinKind.UInt32 => "Int",
                BuiltinKind.Int64 or BuiltinKind.UInt64 => "Long",

                BuiltinKind.Float32 => "Float",
                BuiltinKind.Float64 => "Double",

                BuiltinKind.String => "String",

                BuiltinKind.Any => "GMValue",
                BuiltinKind.AnyArray => "List<GMValue>",
                BuiltinKind.AnyMap => "Map<String, GMValue>",

                BuiltinKind.Function => "GMFunction",

                // Kotlin surface should be idiomatic
                BuiltinKind.Buffer => "ByteArray",

                _ => "Any"
            };
        }

        private static string MapEnum(IrType.Named n) => n.Name;

        private static string MapStruct(IrType.Named n) => n.Name;

        public string MapPassType(IrType type, bool owned = false) => Map(type, owned);
    }
}
