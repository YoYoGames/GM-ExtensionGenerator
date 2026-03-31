using codegencore.Models;
using extgen.Models.Config;

namespace extgen.TypeSystem.Cpp
{
    /// <summary>
    /// Maps IR types to C++ type names with support for owned vs borrowed semantics.
    /// </summary>
    internal sealed class CppTypeMap(RuntimeNaming runtime) : IIrTypeMap
    {
        /// <summary>
        /// Maps an IR type to a C++ type name.
        /// </summary>
        /// <param name="t">The IR type to map.</param>
        /// <param name="owned">
        /// When true, returns owning representation (std::string, streams).
        /// When false, returns view/borrowed representation (string_view, GMValue view types).
        /// </param>
        public string Map(IrType t, bool owned = false)
        {
            // 1) nullable wrapper applies last
            var isNullable = IrType.IsNullable(t);
            t = IrType.StripNullable(t);

            // 2) map non-nullable
            var core = MapNonNullable(t, owned);

            // 3) optional for nullable
            if (isNullable)
                core = $"std::optional<{core}>";

            return core;
        }

        /// <summary>
        /// Maps non-nullable IR types to C++ types.
        /// </summary>
        private string MapNonNullable(IrType t, bool owned)
        {
            return t switch
            {
                // Arrays map structurally
                IrType.Array a => MapArray(a, owned),

                // Named maps to namespaces (enum/struct)
                IrType.Named n => n.Kind switch
                {
                    NamedKind.Enum => $"{runtime.EnumsNamespace}::{n.Name}",
                    NamedKind.Struct => $"{runtime.StructsNamespace}::{n.Name}",
                    _ => n.Name
                },

                // Builtins map via BuiltinKind
                IrType.Builtin b => MapBuiltin(b, owned),

                _ => throw new NotSupportedException($"code emitter: unsupported IrType shape '{t.GetType().Name}'.")
            };
        }

        /// <summary>
        /// Maps array types to std::array (fixed-length) or std::vector (dynamic).
        /// </summary>
        private string MapArray(IrType.Array a, bool owned)
        {
            var elem = Map(a.Element, owned: owned);

            return a.FixedLength is int n
                ? $"std::array<{elem}, {n}>"
                : $"std::vector<{elem}>";
        }

        /// <summary>
        /// Maps builtin types to their C++ equivalents.
        /// </summary>
        private string MapBuiltin(IrType.Builtin b, bool owned)
        {
            var wireNs = runtime.ExtWireNamespace;

            return b.Kind switch
            {
                BuiltinKind.Void => "void",

                BuiltinKind.Bool => "bool",

                BuiltinKind.Int8 => "std::int8_t",
                BuiltinKind.Int16 => "std::int16_t",
                BuiltinKind.Int32 => "std::int32_t",
                BuiltinKind.Int64 => "std::int64_t",

                BuiltinKind.UInt8 => "std::uint8_t",
                BuiltinKind.UInt16 => "std::uint16_t",
                BuiltinKind.UInt32 => "std::uint32_t",
                BuiltinKind.UInt64 => "std::uint64_t",

                BuiltinKind.Float32 => "float",
                BuiltinKind.Float64 => "double",

                BuiltinKind.String => owned ? "std::string" : "std::string_view",

                // Dynamic types (Any/AnyArray/AnyMap) have two representations:
                // - Owned (for returns): Stream types that let you build up data to send back to GML
                // - Borrowed (for params): View types that let you read data recieved from GML
                // This mirrors Rust's owned vs borrowed semantics applied to GameMaker's dynamic types.
                BuiltinKind.Any => owned ? $"{wireNs}::DataStream" : $"{wireNs}::GMValue",
                BuiltinKind.AnyArray => owned ? $"{wireNs}::ArrayStream" : $"{wireNs}::GMArrayView",
                BuiltinKind.AnyMap => owned ? $"{wireNs}::StructStream" : $"{wireNs}::GMObjectView",

                // Buffer and Function types are not supported as owned/return types.
                // Why? Buffers are transient handles that only exist during the call; returning one
                // would leave GML with a dangling reference. Functions similarly can't be "returned"
                // because they're callback handles managed by the GML runtime, not native code.
                BuiltinKind.Buffer => owned
                    ? throw new NotSupportedException("code emitter: buffer as owning/return type is not supported.")
                    : $"{wireNs}::GMBuffer",

                BuiltinKind.Function => owned
                    ? throw new NotSupportedException("code emitter: function as owning/return type is not supported.")
                    : $"{wireNs}::GMFunction",

                _ => throw new NotSupportedException($"code emitter: builtin kind '{b.Kind}' is not supported.")
            };
        }

        /// <summary>
        /// Maps the type used for parameter passing (by value vs const ref).
        /// Determines calling convention based on C++ ergonomics, not ownership transfer.
        /// </summary>
        /// <param name="t">The IR type to map.</param>
        /// <param name="owned">Whether to use owned representation.</param>
        public string MapPassType(IrType t, bool owned = false)
        {
            // Apply nullable stripping for pass-by-ref decision, but keep the mapped type identical.
            var baseType = Map(t, owned);

            // Decide whether this should be passed by const ref.
            // Pragmatic:
            // - structs: const T&
            // - vectors/arrays/string/optional: const T&
            // - scalars/bool: by value
            // - view types (string_view, GMValue, GMArrayView, GMObjectView): by value (cheap handle)
            // - named enums: by value
            if (ShouldPassByConstRef(t, owned))
                return $"const {baseType}&";

            return baseType;
        }

        /// <summary>
        /// Determines whether a type should be passed by const reference.
        /// We pass by const ref for non-trivial types (structs, containers, strings).
        /// Scalars, enums, and view types are passed by value.
        /// </summary>
        private bool ShouldPassByConstRef(IrType t, bool owned)
        {
            // If it's nullable, the wrapper (std::optional<...>) is usually non-trivial.
            if (IrType.IsNullable(t)) 
                t = t.StripNullable();

            return t switch
            {
                IrType.Array => true,

                IrType.Named n => n.Kind == NamedKind.Struct, // enums by value, structs by ref

                IrType.Builtin b => b.Kind switch
                {
                    BuiltinKind.String => owned, // std::string by ref, string_view by value
                    BuiltinKind.Any => true,    // streams are heavier than GMValue handles
                    BuiltinKind.AnyArray => true,
                    BuiltinKind.AnyMap => true,
                    BuiltinKind.Buffer => false,   // GMBuffer handle
                    BuiltinKind.Function => true, // GMFunction handle
                    _ => false
                },

                _ => false
            };
        }
    }
}
