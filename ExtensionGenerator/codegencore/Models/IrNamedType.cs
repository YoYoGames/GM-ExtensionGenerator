using System.Collections.Immutable;

namespace codegencore.Models
{
    public abstract record IrNamedTypeInfo
    {
        public sealed record Struct(string Name) : IrNamedTypeInfo;
        public sealed record Enum(string Name, IrType Underlying) : IrNamedTypeInfo;
        public sealed record Alias(string Name, IrType Target) : IrNamedTypeInfo;
    }
}