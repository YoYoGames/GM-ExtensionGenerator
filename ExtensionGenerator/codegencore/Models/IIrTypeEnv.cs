namespace codegencore.Models
{
    /// <summary>
    /// Type environment for resolving named types (enums, structs).
    /// </summary>
    public interface IIrTypeEnv
    {
        /// <summary>
        /// Attempts to resolve a named type by name.
        /// </summary>
        /// <param name="name">Type name to resolve.</param>
        /// <param name="namedType">Resolved type information if found.</param>
        /// <returns>True if the type was found, false otherwise.</returns>
        bool TryResolveNamed(string name, out IrNamedTypeInfo namedType);
    }
}