using extgen.Model;

namespace extgen.TypeSystem.Java
{
    internal sealed class JavaTypeMap : IIrTypeMap
    {
        public string Map(IrType t, bool owned = false)
        {
            // 1) Base "core" type, without collection / nullability
            string core = t.Kind switch
            {
                IrTypeKind.Void => "void",
                IrTypeKind.Enum => MapEnum(t),
                IrTypeKind.Struct => MapStruct(t),
                IrTypeKind.Scalar => MapScalar(t),
                IrTypeKind.Any => "GMValue",
                IrTypeKind.AnyArray => "java.util.List<GMValue>",
                IrTypeKind.AnyMap => "java.util.Map<String, GMValue>",
                IrTypeKind.Function => "GMFunction",
                IrTypeKind.Buffer => MapBuffer(t),
                IrTypeKind.Variant => "Object",

                _ => "Object"
            };

            // 2) Collections
            if (t.IsCollection)
            {
                var elem = IrHelpers.Element(t);
                var elemType = Map(elem);
                core = MapCollection(t, elemType);
            }

            // 3) Nullable -> Optional<BoxedType>
            if (t.IsNullable)
            {
                core = $"java.util.Optional<{Box(core)}>";
            }

            return core;
        }

        public string MapScalar(IrType t, bool owned = false) => t.Name switch
        {
            "bool" => "boolean",
            "int8" => "byte",
            "int16" => "short",
            "int32" => "int",
            "int64" => "long",
            "uint8" => "byte",
            "uint16" => "short",
            "uint32" => "int",
            "uint64" => "long",
            "float" => "float",
            "double" => "double",
            "string" => MapString(t, owned),
            _ => throw new InvalidDataException("type not valid scalar")
        };

        public string MapEnum(IrType t) => t.Name;
        public string MapStruct(IrType t) => t.Name;
        public string MapString(IrType t, bool owned = false) => "String";
        public string MapBuffer(IrType t) => "ByteBuffer";
        
        public string MapCollection(IrType t, string elementType)
        {
            // fixed-length: T[]   else: List<T>
            return t.FixedLength is int
                ? $"{elementType}[]"
                : $"java.util.List<{Box(elementType)}>";
        }

        // ------------------------------
        // Java type mapping (used by both Java & Kotlin flows)
        // ------------------------------
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

        public string MapPassType(IrType type, bool owned = false)
        {
            return Map(type, owned);
        }
    }
}
