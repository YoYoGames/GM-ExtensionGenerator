namespace extgen.Emitters.Yy
{
    internal sealed class YyLayout
    {
        public string OutputDir { get; }

        public string OutputFile { get; }

        public YyLayout(string root, YyEmitterSettings options)
        {
            var apiOutput = Path.GetFullPath(options.OutputFile, root);

            OutputFile = Path.GetFileNameWithoutExtension(apiOutput);
            OutputDir = Path.GetDirectoryName(apiOutput) ?? root;

            Directory.CreateDirectory(OutputDir);
        }
    }

}