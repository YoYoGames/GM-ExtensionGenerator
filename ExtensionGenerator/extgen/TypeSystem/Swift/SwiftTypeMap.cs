using extgen.Model;
using extgen.Options;

namespace extgen.TypeSystem.Swift
{
    internal sealed class SwiftTypeMap(RuntimeNaming Runtime) : IIrTypeMap
    {
        public string Map(IrType t, bool owned = false)
        {
            // 1) Base "core" type, without collection / nullability
            string core = t.Kind switch
            {
                IrTypeKind.Void => "Void",
                IrTypeKind.Enum => MapEnum(t),
                IrTypeKind.Struct => MapStruct(t),
                IrTypeKind.Scalar => MapScalar(t, owned),
                IrTypeKind.Any => "GMValue",
                IrTypeKind.AnyArray => "[GMValue]",
                IrTypeKind.AnyMap => "[(String, GMValue)]",
                IrTypeKind.Buffer => !owned ? MapBuffer(t) : throw new NotSupportedException("code emitter: buffer as return is not supported."),
                IrTypeKind.Function => "GMFunction",
                IrTypeKind.Variant => throw new NotSupportedException($"code emitter: variants ({t.Name}) are not supported yet."),

                _ => t.Name
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
                core = $"{core}?";
            }

            return core;
        }

        public string MapBuffer(IrType t) => $"GMBuffer";

        public string MapCollection(IrType t, string elementType) => $"[{elementType}]";
        
        public string MapEnum(IrType t) => $"{t.Name}";

        public string MapScalar(IrType t, bool owned = false) => t.Name switch
        {
            "bool" => "Bool",
            "int8" => "Int8",
            "int16" => "Int16",
            "int32" => "Int32",
            "int64" => "Int64",
            "uint8" => "UInt8",
            "uint16" => "UInt16",
            "uint32" => "UInt32",
            "uint64" => "UInt64",
            "float" => "Float",
            "double" => "Double",
            "string" => MapString(t, owned),
            _ => throw new NotSupportedException($"code emitter: scalar type ({t.Name}) is not supported.")
        };

        public string MapString(IrType t, bool owned = false) => "String";

        public string MapStruct(IrType t) => $"{t.Name}";

        public string MapPassType(IrType t, bool owned = false)
        {
            var baseName = Map(t, owned);
            return baseName;
        }
    }
}
