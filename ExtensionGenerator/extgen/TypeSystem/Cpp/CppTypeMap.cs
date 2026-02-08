using codegencore.Models;
using extgen.Models.Config;

namespace extgen.TypeSystem.Cpp
{
    internal sealed class CppTypeMap(RuntimeNaming runtime) : IIrTypeMap
    {
        /// <summary>
        /// Map an IR type to a C++ type name.
        /// owned:
        ///   - true  => owning representation (std::string, streams, etc.)
        ///   - false => view/borrowed representation where applicable (string_view, GMValue view types)
        /// </summary>
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

        private string MapArray(IrType.Array a, bool owned)
        {
            var elem = Map(a.Element, owned: owned);

            return a.FixedLength is int n
                ? $"std::array<{elem}, {n}>"
                : $"std::vector<{elem}>";
        }

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

                // "Any" family: your runtime decides view vs owning streams
                BuiltinKind.Any => owned ? $"{wireNs}::DataStream" : $"{wireNs}::GMValue",
                BuiltinKind.AnyArray => owned ? $"{wireNs}::ArrayStream" : $"{wireNs}::GMArrayView",
                BuiltinKind.AnyMap => owned ? $"{wireNs}::StructStream" : $"{wireNs}::GMObjectView",

                // Buffer/Function: policy decision — you previously disallowed "owned" (return)
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
        /// Map the type used for parameter passing (by value vs const ref).
        /// This does NOT mean ownership transfer; it's about C++ calling convention ergonomics.
        /// </summary>
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

        private bool ShouldPassByConstRef(IrType t, bool owned)
        {
            // If it's nullable, the wrapper (std::optional<...>) is usually non-trivial.
            if (IrType.IsNullable(t)) 
                t = t.NonNull();

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
