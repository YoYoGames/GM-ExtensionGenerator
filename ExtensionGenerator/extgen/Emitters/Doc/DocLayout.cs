namespace extgen.Emitters.Doc
{
    internal sealed class DocLayout
    {
        public string OutputDir { get; }

        public string OutputFile { get; }

        public DocLayout(string root, DocEmitterSettings options)
        {
            OutputDir = Path.GetFullPath(options.OutputFolder, root);
            OutputFile = Path.GetFileNameWithoutExtension(options.OutputFilename);

            Directory.CreateDirectory(OutputDir);
        }
    }
}
