namespace codegencore.Writers.JSDoc
{
    public readonly record struct ParamDoc(string Name, string Type, string? Description = null, bool Optional = false) { }
}

