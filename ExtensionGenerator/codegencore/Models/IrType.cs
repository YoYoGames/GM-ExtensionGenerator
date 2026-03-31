namespace codegencore.Models
{
    /// <summary>
    /// Represents a type in the intermediate representation (IR) system.
    /// Supports builtin types, user-defined types (structs/enums), arrays, and nullable types.
    /// </summary>
    public abstract record IrType
    {
        // Type shapes
        public sealed record Builtin(BuiltinKind Kind) : IrType;
        public sealed record Named(NamedKind Kind, string Name) : IrType;
        public sealed record Array(IrType Element, int? FixedLength = null) : IrType;
        public sealed record Nullable(IrType Underlying) : IrType;

        // Builtin singletons (avoid repeated allocations)
        public static readonly IrType Bool = new Builtin(BuiltinKind.Bool);

        public static readonly IrType Int8 = new Builtin(BuiltinKind.Int8);
        public static readonly IrType UInt8 = new Builtin(BuiltinKind.UInt8);
        public static readonly IrType Int16 = new Builtin(BuiltinKind.Int16);
        public static readonly IrType UInt16 = new Builtin(BuiltinKind.UInt16);
        public static readonly IrType Int32 = new Builtin(BuiltinKind.Int32);
        public static readonly IrType UInt32 = new Builtin(BuiltinKind.UInt32);
        public static readonly IrType Int64 = new Builtin(BuiltinKind.Int64);
        public static readonly IrType UInt64 = new Builtin(BuiltinKind.UInt64);

        public static readonly IrType Float32 = new Builtin(BuiltinKind.Float32);
        public static readonly IrType Float64 = new Builtin(BuiltinKind.Float64);

        public static readonly IrType Float = Float32;
        public static readonly IrType Double = Float64;

        public static readonly IrType String = new Builtin(BuiltinKind.String);
        public static readonly IrType Void = new Builtin(BuiltinKind.Void);

        public static readonly IrType Buffer = new Builtin(BuiltinKind.Buffer);
        public static readonly IrType Function = new Builtin(BuiltinKind.Function);

        public static readonly IrType Any = new Builtin(BuiltinKind.Any);
        public static readonly IrType AnyArray = new Builtin(BuiltinKind.AnyArray);
        public static readonly IrType AnyMap = new Builtin(BuiltinKind.AnyMap);

        /// <summary>
        /// Wraps a type in a nullable container if not already nullable.
        /// </summary>
        public static IrType MakeNullable(IrType t) =>
            t is Nullable ? t : new Nullable(t);

        /// <summary>
        /// Removes the nullable wrapper from a type if present.
        /// </summary>
        public static IrType StripNullable(IrType t) =>
            t is Nullable n ? n.Underlying : t;

        /// <summary>
        /// Determines whether a type is nullable.
        /// </summary>
        public static bool IsNullable(IrType t) => t is Nullable;

        /// <summary>
        /// Creates an array type with the specified element type and optional fixed length.
        /// </summary>
        public static IrType MakeArray(IrType element, int? fixedLength = null) =>
            new Array(element, fixedLength);

        /// <summary>
        /// Creates a stable hash code for structural caching in generators.
        /// </summary>
        public virtual int GetStableHashCode() => GetHashCode();
    }

    public enum BuiltinKind
    {
        // Primitives
        Bool,
        Int8, UInt8, Int16, UInt16, Int32, UInt32, Int64, UInt64,
        Float32, Float64,
        String,
        Void,

        // Special builtins (still just identity; semantics decided by generator)
        Buffer,
        Function,

        // Dynamic-ish builtins (identity only)
        Any,
        AnyArray,
        AnyMap
    }

    public enum NamedKind
    {
        Struct,
        Enum
        // Variant if you really have it as a named symbol
    }
}
