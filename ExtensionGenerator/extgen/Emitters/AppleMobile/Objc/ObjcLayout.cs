using extgen.Emitters.AppleMobile;

namespace extgen.Emitters.AppleMobile.Objc
{
    internal sealed class ObjcLayout
    {
        public string CoreDir { get; }
        public string CodeGenDir { get; }
        public string SourceDir { get; }

        public ObjcLayout(string root, IAppleMobileEmitterSettings options)
        {
            CoreDir = Path.GetFullPath(Path.Combine($"./code_gen/core"), root);
            CodeGenDir = Path.GetFullPath(Path.Combine($"./code_gen/{options.Platform}"), root);
            SourceDir = Path.GetFullPath(Path.Combine($"./src/{options.SourceFolder}"), root);

            if (Directory.Exists(CodeGenDir)) Directory.Delete(CodeGenDir, true);

            Directory.CreateDirectory(CoreDir);
            Directory.CreateDirectory(CodeGenDir);
            Directory.CreateDirectory(SourceDir);
        }
    }
}
