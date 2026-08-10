namespace extgen.Emitters.AppleMobile.Objc
{
    internal sealed class ObjcLayout
    {
        public string CoreDir { get; }
        public string CodeGenDir { get; }
        public string SourceDir { get; }
        public string OutputSource { get; }

        /// <summary>Relative include used by the user shell for Internal headers.</summary>
        public string InternalIncludePrefix { get; }

        public ObjcLayout(string root, IAppleMobileEmitterSettings settings)
        {
            OutputSource = Path.GetFullPath(settings.OutputSourceFolder, root);

            if (settings.SourceLayout == AppleMobileSourceLayout.GameMakerSourceTree)
            {
                // GameMaker picks up ObjC from iOSSource/tvOSSource next to the .yy
                // (same contract as AndroidSource for Java).
                CodeGenDir = Path.Combine(OutputSource, "code_gen", settings.Platform);
                SourceDir = OutputSource;
                InternalIncludePrefix = $"code_gen/{settings.Platform}";
                CoreDir = Path.Combine(OutputSource, "code_gen", "core");
            }
            else
            {
                // CMake path: sources live under the generator root and are packaged into the XCFramework.
                CoreDir = Path.GetFullPath(Path.Combine("./code_gen/core"), root);
                CodeGenDir = Path.GetFullPath(Path.Combine($"./code_gen/{settings.Platform}"), root);
                SourceDir = Path.GetFullPath(Path.Combine($"./src/{settings.SourceFolder}"), root);
                InternalIncludePrefix = settings.Platform;
            }

            if (Directory.Exists(CodeGenDir))
                Directory.Delete(CodeGenDir, true);

            Directory.CreateDirectory(CoreDir);
            Directory.CreateDirectory(CodeGenDir);
            Directory.CreateDirectory(SourceDir);
        }
    }
}
