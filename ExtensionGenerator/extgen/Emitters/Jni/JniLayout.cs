using extgen.Options;

namespace extgen.Emitters.Jni
{
    internal sealed class JniLayout
    {
        public string JavaBaseDir { get; }
        public string NativeBaseDir { get; }

        public JniLayout(string root, JniEmitterOptions opts)
        {
            JavaBaseDir = Path.GetFullPath(opts.OutputJavaFolder, root);
            NativeBaseDir = Path.GetFullPath(opts.OutputNativeFolder, root);

            Directory.CreateDirectory(JavaBaseDir);
            Directory.CreateDirectory(NativeBaseDir);
        }
    }
}
