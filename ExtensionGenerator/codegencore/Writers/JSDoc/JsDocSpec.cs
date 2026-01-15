namespace codegencore.Writers.JSDoc
{
    internal sealed record JsDocSpec(IReadOnlyList<string> Lines, IReadOnlyList<ParamDoc> Params) : IJsDocSpec;
}

