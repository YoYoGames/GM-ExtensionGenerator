using extgen.Emitters.AppleMobile;

namespace extgen.Emitters.AppleMobile.Objc
{
    internal sealed class ObjcLayout
    {
        public string CodeGenDir { get; }
        public string SourceDir { get; }

        public ObjcLayout(string root, IAppleMobileEmitterSettings options)
        {
            CodeGenDir = Path.GetFullPath(Path.Combine($"./code_gen/{options.Platform}"), root);
            SourceDir = Path.GetFullPath(Path.Combine($"./src/{options.SourceFolder}"), root);

            if (Directory.Exists(CodeGenDir)) Directory.Delete(CodeGenDir, true);

            Directory.CreateDirectory(CodeGenDir);
            Directory.CreateDirectory(SourceDir);
        }
    }
}
