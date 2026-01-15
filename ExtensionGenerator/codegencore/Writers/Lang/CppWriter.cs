
namespace codegencore.Writers.Lang
{
    /// <summary>
    /// Well-known linkage names for <c>extern "..."</c>.
    /// Extend as you add other ABIs (e.g. Rust, Wasm, StdCall).
    /// </summary>
    public enum CppLinkage
    {
        C,
        CPP
    }

    /// <summary>Lightweight parameter record used by the new overloads.</summary>
    public readonly record struct Param(string Type, string Name);

    public class CppWriter(ICodeWriter io) : CxxWriter<CppWriter>(io)
    {
        public CppWriter Extern(CppLinkage linkage, Action<CppWriter> expr) => Append($"extern \"{(linkage == CppLinkage.C ? "C" : "C++")}\" ").Block(_ => expr(this));

        public CppWriter ExternBlock(CppLinkage linkage, Action<CppWriter> body) => Line($"extern \"{(linkage == CppLinkage.C ? "C" : "C++")}\"").Block(_ => body(this), true);
    }

}

