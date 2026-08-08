using extgen.Emitters.Utils;
using extgen.Models.Config;

namespace extgen.Emitters.Android.Jni
{
    internal sealed record JniFunctionSpec(
        string Name,
        string ExportName,
        string NativeName,
        IEnumerable<ExportParam> ExportParams,
        ExportType ExportReturnType
    )
    {
        public static JniFunctionSpec From(NativeExportSpec export, RuntimeNaming naming) =>
            new(
                Name: export.FunctionName,
                ExportName: $"{naming.JniPrefix}{export.FunctionName}",
                NativeName: export.NativeSymbol,
                ExportParams: export.Params,
                ExportReturnType: export.ReturnType);
    }
}
