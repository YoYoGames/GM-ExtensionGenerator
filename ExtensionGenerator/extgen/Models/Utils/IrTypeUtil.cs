using codegencore.Models;

namespace extgen.Models.Utils
{
    // =====================================================================
    // Local IrType helpers for the new shape-based IrType
    // (Put this in extgen.Utils if you want to reuse everywhere.)
    // =====================================================================
    internal static class IrTypeUtil
    {
        public static bool IsVoid(IrType t) =>
            t is IrType.Builtin { Kind: BuiltinKind.Void };

        public static bool IsBool(IrType t) =>
            StripNullable(t) is IrType.Builtin { Kind: BuiltinKind.Bool };

        public static bool IsStringScalar(IrType t) =>
            StripNullable(t) is IrType.Builtin { Kind: BuiltinKind.String };

        public static bool IsNumericScalar(IrType t)
        {
            t = StripNullable(t);

            // NOTE: arrays are not numeric scalars at surface
            if (t is IrType.Array) return false;

            return t is IrType.Builtin
            {
                Kind:
                    BuiltinKind.Bool
                    or BuiltinKind.Int8 or BuiltinKind.UInt8
                    or BuiltinKind.Int16 or BuiltinKind.UInt16
                    or BuiltinKind.Int32 or BuiltinKind.UInt32
                    or BuiltinKind.Int64 or BuiltinKind.UInt64
                    or BuiltinKind.Float32 or BuiltinKind.Float64
            };
        }

        public static bool ContainsNullable(IrType t) =>
            t switch
            {
                IrType.Nullable => true,
                IrType.Array a => ContainsNullable(a.Element),
                _ => false
            };

        public static bool ContainsBuiltin(IrType t, BuiltinKind kind) =>
            t switch
            {
                IrType.Builtin b => b.Kind == kind,
                IrType.Nullable n => ContainsBuiltin(n.Underlying, kind),
                IrType.Array a => ContainsBuiltin(a.Element, kind),
                _ => false
            };

        public static IrType StripNullable(IrType t) =>
            t is IrType.Nullable n ? n.Underlying : t;

        public static string ToDebugString(IrType t) =>
            t switch
            {
                IrType.Nullable n => $"optional<{ToDebugString(n.Underlying)}>",
                IrType.Array a => a.FixedLength is int n
                    ? $"{ToDebugString(a.Element)}[{n}]"
                    : $"{ToDebugString(a.Element)}[]",

                IrType.Named { Kind: NamedKind.Struct, Name: var s } => $"struct {s}",
                IrType.Named { Kind: NamedKind.Enum, Name: var e } => $"enum {e}",

                IrType.Builtin b => b.Kind.ToString(),
                _ => t.ToString() ?? "type"
            };
    }
}
