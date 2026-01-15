using extgen.Model;

namespace extgen.TypeSystem.Kotlin
{
    /// <summary>
    /// Maps IrType -> Kotlin types for the public surface (interfaces, etc.).
    /// </summary>
    public sealed class KotlinTypeMap : IIrTypeMap
    {
        public string Map(IrType t, bool owned = false)
        {
            // 1) Base "core" type, without collection / nullability
            string core = t.Kind switch
            {
                IrTypeKind.Void => "Unit",      // only meaningful as return
                IrTypeKind.Enum => MapEnum(t),
                IrTypeKind.Struct => MapStruct(t),
                IrTypeKind.Scalar => MapScalar(t, owned),
                IrTypeKind.Any => "GMValue",
                IrTypeKind.AnyArray => "List<GMValue>",
                IrTypeKind.AnyMap => "Map<String, GMValue>",
                IrTypeKind.Function => "GMFunction",
                IrTypeKind.Buffer => MapBuffer(t),

                _ => "Any"
            };

            // 2) Collections
            if (t.IsCollection)
            {
                var elem = IrHelpers.Element(t);
                var elemType = Map(elem);
                core = MapCollection(t, elemType);
            }

            // 3) Nullable
            if (t.IsNullable)
            {
                core += "?";
            }

            return core;
        }

        public string MapScalar(IrType t, bool owned = false) => t.Name switch
        {
            "bool" => "Boolean",
            "int8" => "Byte",
            "int16" => "Short",
            "int32" => "Int",
            "int64" => "Long",
            "uint8" => "Byte",   // Kotlin/JVM has UInt but on JVM it's still boxed
            "uint16" => "Int",
            "uint32" => "Long",
            "uint64" => "Long",
            "float" => "Float",
            "double" => "Double",
            "string" => MapString(t, owned),

            _ => "Double"
        };

        public string MapEnum(IrType t) => t.Name;
        public string MapStruct(IrType t) => t.Name;
        public string MapString(IrType t, bool owned = false) => "String";

        /// <summary>
        /// We choose ByteArray for Kotlin surface to keep it idiomatic.
        /// The Java bridge still uses ByteBuffer internally.
        /// </summary>
        public string MapBuffer(IrType t) => "ByteArray";

        public string MapCollection(IrType t, string elementType)
        {
            // fixed-length: Array<T>   else: List<T>
            return t.FixedLength is int
                ? $"Array<{elementType}>"
                : $"List<{elementType}>";
        }

        public string MapPassType(IrType type, bool owned = false)
        {
            return Map(type, owned);
        }
    }
}
