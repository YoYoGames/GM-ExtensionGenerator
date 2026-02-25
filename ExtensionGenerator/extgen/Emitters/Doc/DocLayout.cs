namespace extgen.Emitters.Doc
{
    internal sealed class DocLayout
    {
        public string FullPath { get; }

        public string OutputDir { get; }

        public string OutputFile { get; }

        public DocLayout(string root, DocEmitterSettings options)
        {
            FullPath = Path.GetFullPath(options.OutputFile, root);
            OutputFile = Path.GetFileName(FullPath);
            OutputDir = Path.GetDirectoryName(FullPath) ?? Path.GetFullPath("./", root);

            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }
        }
    }
}
