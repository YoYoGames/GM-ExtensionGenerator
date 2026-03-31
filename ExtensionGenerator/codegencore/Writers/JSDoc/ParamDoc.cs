namespace codegencore.Writers.JSDoc
{
    /// <summary>
    /// Represents JSDoc parameter documentation with name, type, description, and optional flag.
    /// </summary>
    public readonly record struct ParamDoc(string Name, string? Type, string? Description = null, bool Optional = false) { }
}

