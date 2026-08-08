using extgen.Options.Android;

namespace extgen.Emitters.Android.Jni
{
    internal sealed class JniLayout
    {
        /// <summary>Extension output root (same as CodegenRunner outputDir / emitter <c>dir</c>).</summary>
        public string OutputRoot { get; }

        public string JavaBaseDir { get; }

        public string JavaCodeGenDir => Path.Combine(JavaBaseDir, "code_gen");

        public string NativeCodeGenDir { get; }

        public bool HasNativeCppDir { get; }

        public JniLayout(string root, AndroidEmitterSettings opts, bool emitNativeCppDir = true)
        {
            OutputRoot = Path.GetFullPath(root);
            JavaBaseDir = Path.GetFullPath(Path.Combine(opts.OutputFolder, "Java"), root);
            NativeCodeGenDir = Path.GetFullPath(opts.OutputNativeFolder, root);
            HasNativeCppDir = emitNativeCppDir;

            if (Directory.Exists(JavaCodeGenDir)) Directory.Delete(JavaCodeGenDir, true);
            if (emitNativeCppDir && Directory.Exists(NativeCodeGenDir)) Directory.Delete(NativeCodeGenDir, true);

            Directory.CreateDirectory(JavaBaseDir);
            Directory.CreateDirectory(JavaCodeGenDir);
            if (emitNativeCppDir)
                Directory.CreateDirectory(NativeCodeGenDir);
        }
    }
}
