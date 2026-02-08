using System.Text;

namespace codegencore.Models
{
    public static class IrTypeExtensions
    {
        public static IrType NonNull(this IrType t) => IrType.StripNullable(t);

        public static bool IsNullable(this IrType t) => IrType.IsNullable(t);

        public static bool IsVarArray(this IrType t) =>
            t.NonNull() is IrType.Array { FixedLength: null };

        public static bool IsFixedArray(this IrType t) =>
            t.NonNull() is IrType.Array { FixedLength: int };

        public static bool IsEnum(this IrType t) =>
            t.NonNull() is IrType.Named { Kind: NamedKind.Enum };

        public static string ToDebugString(this IrType t)
        {
            var sb = new StringBuilder();
            Append(sb, t);
            return sb.ToString();
        }

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