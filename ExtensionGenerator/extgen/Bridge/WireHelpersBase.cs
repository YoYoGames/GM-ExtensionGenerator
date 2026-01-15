using extgen.Model;

namespace extgen.Bridge
{
    /// <summary>
    /// Base abstraction for language-specific wire helpers.
    /// Responsible for decode/encode operations & helper expressions.
    /// </summary>
    public abstract class WireHelpersBase<TWriter>
    {
        /// <summary>
        /// Read a non-collection, non-nullable value.
        /// </summary>
        public abstract string ReadExpr(IrType t, string bufferVar);

        /// <summary>
        /// Write a non-collection, non-nullable value.
        /// </summary>
        public abstract string WriteExpr(IrType t, string bufferVar, string valueExpr);

        /// <summary>
        /// Generate multi-line decode code for a potentially nullable/collection type.
        /// </summary>
        public abstract void DecodeLines(TWriter w, IrType t, string accessor, bool declare, string bufferVar);

        /// <summary>
        /// Generate multi-line encode code for a potentially nullable/collection type.
        /// </summary>
        public abstract void EncodeLines(TWriter w, IrType t, string accessor, string bufferVar);
    }
}
