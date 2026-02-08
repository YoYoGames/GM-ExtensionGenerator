
namespace extgen.Options.Android
{
    public sealed class AndroidEmitterSettings
    {
        public string OutputFolder { get; set; } = "../AndroidSource";

        public string OutputNativeFolder => "./code_gen/android";
    }
}
