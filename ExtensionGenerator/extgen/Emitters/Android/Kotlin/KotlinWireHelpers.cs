using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge;

namespace extgen.Emitters.Android.Kotlin
{
    /// <summary>
    /// Placeholder for Kotlin wire serialization. Methods intentionally throw as Kotlin does not perform direct binary encoding/decoding.
    /// Exists for architecture symmetry.
    /// </summary>
    internal sealed class KotlinWireHelpers : WireHelpersBase<KotlinWriter>
    {
        public override string ReadExpr(IrType t, string buf)
            => throw new InvalidOperationException("Kotlin backend never performs wire reads.");

        public override string WriteExpr(IrType t, string buf, string valueExpr)
            => throw new InvalidOperationException("Kotlin backend never performs wire writes.");

        public override void DecodeLines(KotlinWriter w, IrType t, string accessor, bool declare, string buf)
            => throw new InvalidOperationException("Kotlin backend never performs decode.");

        public override void EncodeLines(KotlinWriter w, IrType t, string accessor, string buf)
            => throw new InvalidOperationException("Kotlin backend never performs encode.");
    }
}
