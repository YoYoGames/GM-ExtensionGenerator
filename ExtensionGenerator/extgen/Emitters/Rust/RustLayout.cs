namespace extgen.Emitters.Rust
{
    internal sealed class RustLayout
    {
        public string RustRoot { get; }
        public string SrcDir => Path.Combine(RustRoot, "src");
        public string GeneratedDir => Path.Combine(SrcDir, "generated");
        public string UserDir => Path.Combine(SrcDir, "user");
        public string WireCrateDir => Path.Combine(RustRoot, "crates", "gm_ext_wire");

        public RustLayout(string outputDir)
        {
            RustRoot = Path.GetFullPath(Path.Combine(outputDir, "rust"));
            Directory.CreateDirectory(GeneratedDir);
            Directory.CreateDirectory(UserDir);
            Directory.CreateDirectory(WireCrateDir);
        }
    }
}
