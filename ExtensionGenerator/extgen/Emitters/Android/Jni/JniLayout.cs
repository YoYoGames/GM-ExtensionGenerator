using extgen.Options.Android;

namespace extgen.Emitters.Android.Jni
{
    internal sealed class JniLayout
    {
        public string JavaBaseDir { get; }

        public string JavaCodeGenDir => Path.Combine(JavaBaseDir, "code_gen");

        public string NativeCodeGenDir { get; }

        public JniLayout(string root, AndroidEmitterSettings opts)
        {
            JavaBaseDir = Path.GetFullPath(Path.Combine(opts.OutputFolder, "Java"), root);
            NativeCodeGenDir = Path.GetFullPath(opts.OutputNativeFolder, root);

            if (Directory.Exists(NativeCodeGenDir)) Directory.Delete(NativeCodeGenDir, true);
            if (Directory.Exists(JavaCodeGenDir)) Directory.Delete(JavaCodeGenDir, true);

            Directory.CreateDirectory(JavaBaseDir);
            Directory.CreateDirectory(JavaCodeGenDir);
            Directory.CreateDirectory(NativeCodeGenDir);
        }
    }
}
