namespace extgen.Models.Utils
{
    internal static class IrFunctionUtil
    {
        public static IrFunction PatchStructMethod(IrStruct s, IrFunction f)
        {
            if (f.Self is null) return f;

            return f with
            {
                Name = $"{s.Name}__{f.Name}",
                Parameters = [f.Self, ..f.Parameters]
            };
        }
    }
}
