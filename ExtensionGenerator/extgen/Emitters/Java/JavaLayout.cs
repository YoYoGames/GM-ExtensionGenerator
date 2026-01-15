using extgen.Options;

namespace extgen.Emitters.Java
{
    internal sealed class JavaLayout
    {
        public string BaseDir { get; }
        public string Enums => Path.Combine(BaseDir, "enums");
        public string Records => Path.Combine(BaseDir, "records");
        public string Codecs => Path.Combine(BaseDir, "codecs");

        public JavaLayout(string root, JavaEmitterOptions opts)
        {
            BaseDir = Path.GetFullPath(opts.OutputJavaFolder, root);

            Directory.CreateDirectory(BaseDir);
            Directory.CreateDirectory(Enums);
            Directory.CreateDirectory(Records);
            Directory.CreateDirectory(Codecs);
        }
    }
}
