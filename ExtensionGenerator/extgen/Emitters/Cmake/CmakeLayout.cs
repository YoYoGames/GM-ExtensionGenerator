namespace extgen.Emitters.Cmake
{
    internal sealed class CmakeLayout
    {
        public string RootDir { get; }
        public string SourceDir { get; }
        public string ThirdPartyDir { get; }
        public string ScriptsDir { get; }

        public CmakeLayout(string root)
        {
            RootDir = Path.GetFullPath(Path.Combine($"./"), root);
            SourceDir = Path.GetFullPath(Path.Combine($"./src"), root);
            ScriptsDir = Path.GetFullPath(Path.Combine($"./cmake"), root);
            ThirdPartyDir = Path.GetFullPath(Path.Combine($"./third_party"), root);

            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(ThirdPartyDir);
        }
    }
}
