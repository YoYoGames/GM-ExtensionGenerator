using extgencore.Helpers;
using System.Collections.Immutable;

namespace extgen.Model
{
    public sealed record IrType(
        IrTypeKind Kind,
        string Name,
        bool IsCollection = false,
        int? FixedLength = null,
        bool IsNullable = false,
        IrType? Underlying = null)
    {
        public static readonly IrType Void = new(IrTypeKind.Void, "void");
        public static readonly IrType String = new (IrTypeKind.Scalar, "string");

        public static readonly IrType Double = new (IrTypeKind.Scalar, "double");
        public static readonly IrType Float = new (IrTypeKind.Scalar, "float");
        public static readonly IrType Int32 = new (IrTypeKind.Scalar, "int32");
        public static readonly IrType UInt32 = new (IrTypeKind.Scalar, "uint32");
        public static readonly IrType Int64 = new (IrTypeKind.Scalar, "int64");
        public static readonly IrType UInt64 = new (IrTypeKind.Scalar, "uint64");
        public static readonly IrType Int8 = new (IrTypeKind.Scalar, "int8");
        public static readonly IrType UInt8 = new (IrTypeKind.Scalar, "uint8");
        public static readonly IrType Bool = new (IrTypeKind.Scalar, "bool");
        public static readonly IrType Buffer = new (IrTypeKind.Buffer, "bool");

        public static readonly IrType Any = new(IrTypeKind.Any, "any");
        public static readonly IrType AnyArray = new(IrTypeKind.AnyArray, "any");
        public static readonly IrType AnyMap = new(IrTypeKind.AnyMap, "any");
        public static readonly IrType Function = new(IrTypeKind.Function, "function");

        public ImmutableArray<IrType> Variants = ImmutableArray<IrType>.Empty;

        //  GameMaker passes *numbers* as double. A uint64 cannot be represented
        //  exactly, so we exclude it from the fast‑path.
        private static bool IsDoubleFriendly(string scalar)
            => ScalarTypes.IsNumeric(scalar) && !scalar.Equals("uint64", StringComparison.OrdinalIgnoreCase);

        public bool IsNumericScalar => Kind == IrTypeKind.Scalar && IsDoubleFriendly(Name) && !IsCollection;
        public bool IsStringScalar => Kind == IrTypeKind.Scalar && Name.Equals("string", StringComparison.OrdinalIgnoreCase) && !IsCollection;

        public bool IsAggregate => Kind is IrTypeKind.Struct or IrTypeKind.Variant or IrTypeKind.AnyArray or IrTypeKind.AnyMap or IrTypeKind.Function || IsCollection;

        public bool IsVariant => Kind == IrTypeKind.Variant && Name.Equals("variant", StringComparison.OrdinalIgnoreCase);

        //public bool IsEnum => EnumLiterals is { Length: > 0 };

        public bool IsDirectGameMakerArg => IsNumericScalar || IsStringScalar;

    }

}
