using extgen.Models;

namespace extgen.Emitters
{
    public interface IIrEmitter { void Emit(IrCompilation comp, string outputDir); }
}
