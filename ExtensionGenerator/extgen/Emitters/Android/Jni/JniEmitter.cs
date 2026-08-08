using codegencore.Writers.Lang;
using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Options.Android;
using extgen.Utils;

namespace extgen.Emitters.Android.Jni
{
    /// <summary>
    /// Coordinates shared JNI Java emission with a pluggable native bridge (C++ or Rust).
    /// </summary>
    internal sealed class JniEmitter : IIrEmitter
    {
        private readonly AndroidEmitterSettings _settings;
        private readonly RuntimeNaming _runtime;
        private readonly IJniNativeBridgeEmitter _nativeBridge;
        private readonly bool _emitNativeCppDir;

        public JniEmitter(AndroidEmitterSettings settings, RuntimeNaming runtime)
            : this(settings, runtime, new CppJniBridgeEmitter(settings, runtime), emitNativeCppDir: true)
        {
        }

        public JniEmitter(
            AndroidEmitterSettings settings,
            RuntimeNaming runtime,
            IJniNativeBridgeEmitter nativeBridge,
            bool emitNativeCppDir)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _nativeBridge = nativeBridge ?? throw new ArgumentNullException(nameof(nativeBridge));
            _emitNativeCppDir = emitNativeCppDir;
        }

        public void Emit(IrCompilation comp, string dir)
        {
            var ctx = new JniEmitterContext(comp.Name, _settings, _runtime);
            var specs = NativeExportSpec.FromCompilation(comp, _runtime)
                .Select(e => JniFunctionSpec.From(e, _runtime))
                .ToArray();

            var layout = new JniLayout(dir, _settings, emitNativeCppDir: _emitNativeCppDir);
            JniJavaLayer.Emit(ctx, comp, specs, layout);
            _nativeBridge.EmitBridge(ctx, comp, specs, layout);
        }
    }
}
