namespace extgen.Models.Utils
{
    internal static class IrFunctionUtil
    {
        public static IrFunction PatchStructMethod(IrStruct s, IrFunction f) =>
            f with { Name = $"{s.Name}__{f.Name}", Parameters = f.FullParameters };
    }
}
