using codegencore.Models;

namespace extgen.TypeSystem.Java
{
    internal sealed class JavaTypeMap : IIrTypeMap
    {
        public string Map(IrType t, bool owned = false)
        {
            // 1) unwrap nullable (we apply Optional<> at the end)
            var isNullable = IrType.IsNullable(t);
            t = IrType.StripNullable(t);

            // 2) map the non-nullable type
            var core = MapNonNullable(t, owned);

            // 3) apply Optional<...> for nullable
            if (isNullable)
                core = $"java.util.Optional<{Box(core)}>";

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

                // If you add more shapes later, you'll be forced to update here.
                _ => "Object"
            };
        }

        private string MapArray(IrType.Array a, bool owned)
        {
            // Fixed-length => T[] else List<T>
            // Note: Array element may itself be nullable; preserve by mapping element recursively.
            var elem = Map(a.Element, owned);

            return a.FixedLength is int
                ? $"{elem}[]"
                : $"java.util.List<{Box(elem)}>";
        }

        private string MapBuiltin(IrType.Builtin b, bool owned)
        {
            return b.Kind switch
            {
                BuiltinKind.Void => "void",

                BuiltinKind.Bool => "boolean",

                BuiltinKind.Int8 or BuiltinKind.UInt8 => "byte",
                BuiltinKind.Int16 or BuiltinKind.UInt16 => "short",
                BuiltinKind.Int32 or BuiltinKind.UInt32 => "int",
                BuiltinKind.Int64 or BuiltinKind.UInt64 => "long",

                BuiltinKind.Float32 => "float",
                BuiltinKind.Float64 => "double",

                BuiltinKind.String => MapString(owned),

                // Your earlier mapping: Any is GMValue, and AnyArray/AnyMap map to generic containers
                BuiltinKind.Any => "GMValue",
                BuiltinKind.AnyArray => "java.util.List<GMValue>",
                BuiltinKind.AnyMap => "java.util.Map<String, GMValue>",

                BuiltinKind.Function => "GMFunction",
                BuiltinKind.Buffer => "java.nio.ByteBuffer",

                _ => "Object"
            };
        }

        private static string MapEnum(IrType.Named n) => n.Name;

        private static string MapStruct(IrType.Named n) => n.Name;

        private static string MapString(bool owned) => "String";

        private static string Box(string primitive) => primitive switch
        {
            "boolean" => "Boolean",
            "byte" => "Byte",
            "short" => "Short",
            "int" => "Integer",
            "long" => "Long",
            "float" => "Float",
            "double" => "Double",
            _ => primitive
        };

        public string MapPassType(IrType type, bool owned = false) => Map(type, owned);
    }
}
