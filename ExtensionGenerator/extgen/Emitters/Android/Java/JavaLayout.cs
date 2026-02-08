using extgen.Options.Android;

namespace extgen.Emitters.Android.Java
{
    internal sealed class JavaLayout
    {
        public string BaseDir { get; }
        public string CodeGenDir => Path.Combine(BaseDir, "code_gen");

        public string Enums => Path.Combine(CodeGenDir, "enums");
        public string Records => Path.Combine(CodeGenDir, "records");
        public string Codecs => Path.Combine(CodeGenDir, "codecs");

        public JavaLayout(string root, AndroidEmitterSettings opts)
        {
            BaseDir = Path.GetFullPath(Path.Combine(opts.OutputFolder, "Java"), root);
            
            if (Directory.Exists(CodeGenDir))  Directory.Delete(CodeGenDir, true);

            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(CodeGenDir);
            Directory.CreateDirectory(Enums);
            Directory.CreateDirectory(Records);
            Directory.CreateDirectory(Codecs);
        }
    }
}
