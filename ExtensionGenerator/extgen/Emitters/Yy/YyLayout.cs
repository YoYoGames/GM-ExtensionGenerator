using extgen.Options;


namespace extgen.Emitters.Yy
{
    internal sealed class YyLayout
    {
        public string OutputDir { get; }

        public string OutputFile { get; }

        public YyLayout(string root, YyEmitterOptions options)
        {
            OutputDir = Path.GetFullPath(options.OutputFolder, root);
            OutputFile = Path.GetFileNameWithoutExtension(options.OutputFilename);

            Directory.CreateDirectory(OutputDir);
        }
    }

}