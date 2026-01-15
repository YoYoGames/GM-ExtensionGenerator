namespace extgen.Emitters.Cmake
{
    internal sealed class CmakeLayout
    {
        public string RootDir { get; }
        public string SourceDir { get; }
        public string ThirdPartyDir { get; }

        public CmakeLayout(string root)
        {
            RootDir = Path.GetFullPath(Path.Combine($"./"), root);
            SourceDir = Path.GetFullPath(Path.Combine($"./src"), root);
            ThirdPartyDir = Path.GetFullPath(Path.Combine($"./third_party"), root);

            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(ThirdPartyDir);
        }
    }
}
