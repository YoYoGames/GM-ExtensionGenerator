using System.Collections.Immutable;

namespace codegencore.Models
{
    /// <summary>
    /// Represents information about a named type in the intermediate representation.
    /// </summary>
    public abstract record IrNamedTypeInfo
    {
        /// <summary>
        /// Represents a struct type with a name.
        /// </summary>
        public sealed record Struct(string Name) : IrNamedTypeInfo;

        /// <summary>
        /// Represents an enum type with a name and underlying type.
        /// </summary>
        public sealed record Enum(string Name, IrType Underlying) : IrNamedTypeInfo;

        /// <summary>
        /// Represents a type alias with a name and target type.
        /// </summary>
        public sealed record Alias(string Name, IrType Target) : IrNamedTypeInfo;
    }
}