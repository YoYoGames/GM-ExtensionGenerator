using extgen.Options;

namespace extgen.Emitters.Cpp
{
    internal sealed class CppLayout
    {
        public string CoreDir { get; }
        public string CodeGenDir { get; }
        public string SourceDir { get; }

        public CppLayout(string root, CppEmitterOptions options)
        {
            CoreDir = Path.GetFullPath(Path.Combine($"./code_gen/core"), root);
            CodeGenDir = Path.GetFullPath(Path.Combine($"./code_gen/native"), root);
            SourceDir = Path.GetFullPath(Path.Combine($"./src/{options.SourceFolder}"), root);

            if (Directory.Exists(CodeGenDir)) Directory.Delete(CodeGenDir, true);

            Directory.CreateDirectory(CodeGenDir);
            Directory.CreateDirectory(SourceDir);
        }
    }
}
