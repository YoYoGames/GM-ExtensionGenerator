using codegencore.Models;

namespace extgen.TypeSystem
{
    /// <summary>
    /// Cross-language mapping from IrType to language-specific type names.
    /// </summary>
    public interface IIrTypeMap
    {
        /// <summary>
        /// Maps a type for general usage (parameters, fields).
        /// </summary>
        /// <param name="t">IR type to map.</param>
        /// <param name="owned">Whether the type is owned (affects reference semantics).</param>
        /// <returns>Language-specific type name.</returns>
        string Map(IrType t, bool owned = false);

        /// <summary>
        /// Maps a type for parameter passing (may differ from storage type).
        /// </summary>
        /// <param name="type">IR type to map.</param>
        /// <param name="owned">Whether the type is owned (affects reference semantics).</param>
        /// <returns>Language-specific parameter type name.</returns>
        string MapPassType(IrType type, bool owned = false);
    }
}
