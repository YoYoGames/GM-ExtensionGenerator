using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;

namespace extgen.Emitters.Utils
{
    /// <summary>
    /// Portable GameMaker C ABI export for one IR function.
    /// Shared by Cpp, Rust, JNI, and ObjC native emitters.
    /// </summary>
    internal sealed record NativeExportSpec(
        string FunctionName,
        string NativeSymbol,
        IReadOnlyList<ExportParam> Params,
        ExportType ReturnType,
        bool NeedsArgsBuffer,
        bool NeedsRetBuffer)
    {
        public static NativeExportSpec From(IrFunction fn, RuntimeNaming naming) =>
            new(
                FunctionName: fn.Name,
                NativeSymbol: $"{naming.NativePrefix}{fn.Name}",
                Params: ExportTypeUtils.ParamsFor(fn, naming).ToList(),
                ReturnType: ExportTypeUtils.ReturnFor(fn),
                NeedsArgsBuffer: IrAnalysis.NeedsArgsBuffer(fn),
                NeedsRetBuffer: IrAnalysis.NeedsRetBuffer(fn));

        public static IReadOnlyList<NativeExportSpec> FromCompilation(IrCompilation comp, RuntimeNaming naming) =>
            comp.GetAllFunctions(IrFunctionUtil.PatchStructMethod)
                .Select(fn => From(fn, naming))
                .ToList();
    }
}
