using extgen.Model;
using extgen.Options;

namespace extgen.TypeSystem.Objc
{
    internal sealed class ObjcTypeMap(RuntimeNaming Runtime) : IIrTypeMap
    {
        public string Map(IrType t, bool owned = false)
        {
            var wireNs = Runtime.ExtWireNamespace;

            // 1) Base "core" type, without collection / nullability
            string core = t.Kind switch
            {
                IrTypeKind.Void => "void",
                IrTypeKind.Enum => MapEnum(t),
                IrTypeKind.Struct => MapStruct(t),
                IrTypeKind.Scalar => MapScalar(t, owned),
                IrTypeKind.Any => owned ? $"{wireNs}::ValueStream" : $"{wireNs}::GMValue",
                IrTypeKind.AnyArray => owned ? $"{wireNs}::ArrayStream" : $"{wireNs}::GMArrayView",
                IrTypeKind.AnyMap => owned ? $"{wireNs}::StructStream" : $"{wireNs}::GMObjectView",
                IrTypeKind.Buffer => !owned ? MapBuffer(t) : throw new NotSupportedException("code emitter: buffer as return is not supported."),
                IrTypeKind.Function => !owned ? $"{wireNs}::GMFunction" : throw new NotSupportedException("code emitter: function as return is not supported."),
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
                core = $"std::optional<{core}>";
            }

            return core;
        }

        public string MapBuffer(IrType t) => $"{Runtime.ExtWireNamespace}::GMBuffer";

        public string MapCollection(IrType t, string elementType)
        {
            return t.FixedLength is int n
                ? $"std::array<{elementType}, {n}>"
                : $"std::vector<{elementType}>";
        }

        public string MapEnum(IrType t) => $"{Runtime.EnumsNamespace}::{t.Name}";

        public string MapScalar(IrType t, bool owned = false) => t.Name switch
        {
            "bool" => "bool",
            "int8" => "std::int8_t",
            "int16" => "std::int16_t",
            "int32" => "std::int32_t",
            "int64" => "std::int64_t",
            "uint8" => "std::uint8_t",
            "uint16" => "std::uint16_t",
            "uint32" => "std::uint32_t",
            "uint64" => "std::uint64_t",
            "float" => "float",
            "double" => "double",
            "string" => MapString(t, owned),
            _ => throw new NotSupportedException($"code emitter: scalar type ({t.Name}) is not supported.")
        };

        public string MapString(IrType t, bool owned = false) => owned ? "std::string" : "std::string_view";

        public string MapStruct(IrType t) => $"{Runtime.StructsNamespace}::{t.Name}";

        public string MapPassType(IrType t, bool owned = false)
        {
            var baseName = Map(t, owned);
            return t.IsAggregate && !owned ? $"const {baseName}&" : baseName;
        }
    }
}
