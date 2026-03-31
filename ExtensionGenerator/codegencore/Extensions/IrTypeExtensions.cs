using System.Text;

namespace codegencore.Models
{
    public static class IrTypeExtensions
    {
        public static IrType StripNullable(this IrType t) => IrType.StripNullable(t);

        public static bool IsVoid(this IrType t) =>
            t is IrType.Builtin { Kind: BuiltinKind.Void };

        public static bool IsBool(this IrType t) =>
            t.StripNullable() is IrType.Builtin { Kind: BuiltinKind.Bool };

        public static bool IsNullable(this IrType t) => IrType.IsNullable(t);

        public static bool IsStringScalar(this IrType t) =>
            t.StripNullable() is IrType.Builtin { Kind: BuiltinKind.String };

        public static bool IsNumericScalar(this IrType t)
        {
            t = t.StripNullable();

            // Explicit check: arrays are NOT scalars even if their element type is numeric.
            // This matters for parameter passing logic; int[] must use buffer protocol,
            // but int can be passed as a direct double argument.
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

        public static bool IsVarArray(this IrType t) =>
            t.StripNullable() is IrType.Array { FixedLength: null };

        public static bool IsFixedArray(this IrType t) =>
            t.StripNullable() is IrType.Array { FixedLength: int };

        public static bool IsEnum(this IrType t) =>
            t.StripNullable() is IrType.Named { Kind: NamedKind.Enum };

        public static string ToDebugString(this IrType t) =>
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

        public static bool ContainsBuiltin(this IrType t, BuiltinKind kind)
        {
            // search through Nullable/Array nesting
            return t switch
            {
                IrType.Builtin b => b.Kind == kind,
                IrType.Nullable n => n.Underlying.ContainsBuiltin(kind),
                IrType.Array a => a.Element.ContainsBuiltin(kind),
                _ => false
            };
        }

        public static bool ContainsNullable(this IrType t) =>
            t switch
            {
                IrType.Nullable => true,
                IrType.Array a => ContainsNullable(a.Element),
                _ => false
            };

        private static void Append(StringBuilder sb, IrType t)
        {
            switch (t)
            {
                case IrType.Nullable o:
                    Append(sb, o.Underlying);
                    sb.Append('?');
                    return;

                case IrType.Array a:
                    Append(sb, a.Element);
                    sb.Append('[');
                    if (a.FixedLength is int l) sb.Append(l);
                    sb.Append(']');
                    return;

                case IrType.Builtin b:
                    sb.Append(BuiltinName(b.Kind));
                    return;

                case IrType.Named n:
                    // include kind if you want better diagnostics
                    // sb.Append(n.Kind).Append(' ');
                    sb.Append(n.Name);
                    return;

                default:
                    sb.Append("any");
                    return;
            }
        }

        private static string BuiltinName(BuiltinKind k) => k switch
        {
            BuiltinKind.Bool => "bool",
            BuiltinKind.Int8 => "int8",
            BuiltinKind.UInt8 => "uint8",
            BuiltinKind.Int16 => "int16",
            BuiltinKind.UInt16 => "uint16",
            BuiltinKind.Int32 => "int32",
            BuiltinKind.UInt32 => "uint32",
            BuiltinKind.Int64 => "int64",
            BuiltinKind.UInt64 => "uint64",
            BuiltinKind.Float32 => "float32",
            BuiltinKind.Float64 => "float64",
            BuiltinKind.String => "string",
            BuiltinKind.Void => "void",
            BuiltinKind.Buffer => "buffer",
            BuiltinKind.Function => "function",
            BuiltinKind.Any => "any",
            BuiltinKind.AnyArray => "array",
            BuiltinKind.AnyMap => "object",
            _ => k.ToString().ToLowerInvariant()
        };
    }
}