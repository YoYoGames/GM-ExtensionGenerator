using extgen.Model;

namespace extgen.TypeSystem
{
    /// <summary>
    /// Cross-language mapping from IrType -> language-specific type name.
    /// </summary>
    public interface IIrTypeMap
    {
        /// <summary>
        /// Map a type for general usage (parameters, fields).
        /// </summary>
        string Map(IrType t, bool owned = false);

        string MapScalar(IrType t, bool owned = false);
        string MapEnum(IrType t);
        string MapStruct(IrType t);
        string MapString(IrType t, bool owned = false);
        string MapBuffer(IrType t);
        string MapCollection(IrType t, string elementType);
        string MapPassType(IrType type, bool owned = false);
    }
}
