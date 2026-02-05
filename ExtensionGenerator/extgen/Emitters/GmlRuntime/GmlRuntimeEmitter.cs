using extgen.Model;
using extgen.Options;
using extgen.Utils;

namespace extgen.Emitters.GmlRuntime
{
    public sealed class GmlRuntimeEmitter(GmlRuntimeEmitterOptions options) : IIrEmitter
    {
        private readonly GmlRuntimeEmitterOptions _options = options ?? throw new ArgumentNullException(nameof(options));

        public void Emit(IrCompilation comp, string dir)
        {
            var layout = new GmlRuntimeLayout(dir, _options);
            var output = Path.Combine(layout.OutputFolder, $"{layout.OutputFile}.gml");
            ResourceWriter.WriteTextResource(typeof(Program).Assembly, "extgen.Resources.Gml.ExtensionCore_api.gml", output);
        }
    }
}
