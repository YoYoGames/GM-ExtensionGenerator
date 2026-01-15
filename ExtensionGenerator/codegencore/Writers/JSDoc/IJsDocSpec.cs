namespace codegencore.Writers.JSDoc
{
    public interface IJsDocSpec
    {
        IReadOnlyList<string> Lines { get; }    // raw lines to emit
        IReadOnlyList<ParamDoc> Params { get; }    // pulled out params
    }
}

