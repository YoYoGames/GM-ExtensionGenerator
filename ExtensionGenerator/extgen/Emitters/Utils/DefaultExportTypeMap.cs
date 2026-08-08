using codegencore.Models;

namespace extgen.Emitters.Utils
{
    /// <summary>
    /// Interface for classifying IR types into export types.
    /// </summary>
    internal interface IExportTypeMap
    {
        /// <summary>
        /// Classifies an IR type into an export type.
        /// </summary>
        ExportType Classify(IrType t);
    }

    /// <summary>
    /// Default export type classification strategy.
    /// Maps strings → String, pointers → Pointer, everything else → Double.
    /// </summary>
    internal sealed class DefaultExportTypeMap : IExportTypeMap
    {
        /// <inheritdoc />
        public ExportType Classify(IrType t)
        {
            if (t is IrType.Builtin { Kind: BuiltinKind.String })
                return ExportType.String;

            if (t is IrType.Builtin { Kind: BuiltinKind.Pointer })
                return ExportType.Pointer;

            return ExportType.Double;
        }
    }
}
