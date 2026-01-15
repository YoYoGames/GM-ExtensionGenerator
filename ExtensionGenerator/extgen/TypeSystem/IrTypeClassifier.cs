using extgen.Model;

namespace extgen.TypeSystem
{
    public static class IrTypeClassifier
    {
        public static bool IsNumeric(IrType t)
            => t.IsNumericScalar;

        public static bool IsString(IrType t)
            => t.IsStringScalar;

        public static bool IsEnum(IrType t)
            => t.Kind == IrTypeKind.Enum;

        public static bool IsStruct(IrType t)
            => t.Kind == IrTypeKind.Struct;

        public static bool IsBuffer(IrType t)
            => t.Kind == IrTypeKind.Buffer;

        public static bool IsCollection(IrType t)
            => t.IsCollection;
    }
}
