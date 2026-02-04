using extgen.Model;
using extgen.Options;
using extgen.Utils;

namespace extgen.Emitters.GmlRuntime
{
    public sealed class GmlRuntimeEmitter(GmlRuntimeEmitterOptions opts) : IIrEmitter
    {
        private readonly GmlRuntimeEmitterOptions _opts = opts ?? throw new ArgumentNullException(nameof(opts));

        public void Emit(IrCompilation comp, string dir)
        {
            string outputFile = Environment.ExpandEnvironmentVariables(_opts.OutputFile);
            outputFile = Path.GetFullPath(outputFile, dir);
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Gml.ext_core_api.gml", outputFile);
        }
    }
}
