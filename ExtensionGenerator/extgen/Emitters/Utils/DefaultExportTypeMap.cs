using extgen.Model;

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
            if (t.IsNumericScalar) return ExportType.Double;
            if (t.IsStringScalar) return ExportType.String;
            if (t.Kind == IrTypeKind.Enum) return ExportType.Double;

            return ExportType.Double; // conservative fallback
        }
    }
}
