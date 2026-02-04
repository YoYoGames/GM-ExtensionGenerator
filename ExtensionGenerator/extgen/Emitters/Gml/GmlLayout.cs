using extgen.Options;

namespace extgen.Emitters.Gml
{
    internal sealed class GmlLayout
    {
        public string OutputFile { get; }
        public string OutputFolder { get; }

        public GmlLayout(string root, GmlEmitterOptions options)
        {
            var fullpath = Path.GetFullPath(options.OutputFile, root);

            if (!File.Exists(fullpath))
                throw new ArgumentException($"GML Emitter: output file path doesn't exist ({fullpath}).");

            OutputFile = Path.GetFileNameWithoutExtension(fullpath);
            OutputFolder = Path.GetDirectoryName(fullpath) ?? root;
        }
    }
}
