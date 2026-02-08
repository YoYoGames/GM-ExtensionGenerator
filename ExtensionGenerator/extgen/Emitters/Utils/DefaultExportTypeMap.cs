using codegencore.Models;

namespace extgen.Emitters.Utils
{
    internal interface IExportTypeMap
    {
        ExportType Classify(IrType t);
    }

    internal sealed class DefaultExportTypeMap : IExportTypeMap
    {
        public ExportType Classify(IrType t)
        {
            if (t is IrType.Builtin { Kind: BuiltinKind.String }) return ExportType.String;

            return ExportType.Double; // conservative fallback
        }
    }
}
