using extgen.Options;

namespace extgen.Emitters.Doc
{
    internal sealed class DocLayout
    {
        public string OutputDir { get; }

        public DocLayout(string root, DocEmitterOptions options)
        {
            OutputDir = Path.GetFullPath(options.OutputFolder, root);

            Directory.CreateDirectory(OutputDir);
        }
    }
}
