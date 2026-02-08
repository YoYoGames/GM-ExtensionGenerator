namespace extgen.Emitters.Gml
{
    internal sealed class GmlLayout
    {
        public string OutputFile { get; }
        public string OutputFolder { get; }

        public string RuntimeOutputFile { get; }
        public string RuntimeOutputFolder { get; }

        public GmlLayout(string root, GmlEmitterSettings options)
        {
            var apiOutput = Path.GetFullPath(options.OutputFile, root);

            OutputFile = Path.GetFileNameWithoutExtension(apiOutput);
            OutputFolder = Path.GetDirectoryName(apiOutput) ?? root;

            var runtimeOutput = Path.GetFullPath(options.RuntimeFile, root);
            RuntimeOutputFile = Path.GetFileNameWithoutExtension(runtimeOutput);
            RuntimeOutputFolder = Path.GetDirectoryName(runtimeOutput) ?? root;
        }
    }
}
