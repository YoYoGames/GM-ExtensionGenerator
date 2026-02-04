using extgen.Options;

namespace extgen.Emitters.Objc
{
    internal sealed class ObjcLayout
    {
        public string CodeGenDir { get; }
        public string SourceDir { get; }

        public ObjcLayout(string root, IObjcEmitterOptions options)
        {
            CodeGenDir = Path.GetFullPath(Path.Combine($"./code_gen/{options.Platform}"), root);
            SourceDir = Path.GetFullPath(Path.Combine($"./src/{options.SourceFolder}"), root);

            if (Directory.Exists(CodeGenDir)) Directory.Delete(CodeGenDir, true);

            Directory.CreateDirectory(CodeGenDir);
            Directory.CreateDirectory(SourceDir);
        }
    }
}
