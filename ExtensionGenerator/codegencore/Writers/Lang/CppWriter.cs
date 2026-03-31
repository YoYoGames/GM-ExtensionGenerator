
namespace codegencore.Writers.Lang
{
    /// <summary>
    /// Well-known linkage names for <c>extern "..."</c>.
    /// </summary>
    public enum CppLinkage
    {
        C,
        CPP
    }

    /// <summary>
    /// Lightweight parameter record for function parameters.
    /// </summary>
    public readonly record struct Param(string Type, string Name);

    /// <summary>
    /// Provides a fluent API for generating C++ source code with support for extern linkage specifications.
    /// </summary>
    public class CppWriter(ICodeWriter io) : CxxWriter<CppWriter>(io)
    {
        /// <summary>
        /// Writes an extern linkage expression.
        /// </summary>
        public CppWriter Extern(CppLinkage linkage, Action<CppWriter> expr) => Append($"extern \"{(linkage == CppLinkage.C ? "C" : "C++")}\" ").Block(_ => expr(this));

        /// <summary>
        /// Writes an extern linkage block.
        /// </summary>
        public CppWriter ExternBlock(CppLinkage linkage, Action<CppWriter> body) => Line($"extern \"{(linkage == CppLinkage.C ? "C" : "C++")}\"").Block(_ => body(this), true);
    }

}

