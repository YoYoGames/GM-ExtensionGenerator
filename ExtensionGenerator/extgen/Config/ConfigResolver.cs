using extgen.Models.Config;
using extgen.Planning;

namespace extgen.Config
{
    public static class ConfigResolver
    {
        public static ResolvedConfig Resolve(ExtGenConfig cfg, string configPath, Func<string?, string, string> resolvePath)
        {
            ArgumentNullException.ThrowIfNull(cfg);
            if (string.IsNullOrWhiteSpace(configPath)) throw new ArgumentException("configPath is empty.", nameof(configPath));
            ArgumentNullException.ThrowIfNull(resolvePath);

            var fullConfigPath = Path.GetFullPath(configPath);
            var baseDir = Path.GetDirectoryName(fullConfigPath)!;

            var inputPath = resolvePath(cfg.Input, baseDir);
            var outputDir = resolvePath(cfg.Root, baseDir);

            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
                throw new InvalidOperationException($"Input file not found: {inputPath}");
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new InvalidOperationException("Missing 'root' (output directory) in config.");

            Directory.CreateDirectory(outputDir);

            var resolved = new ResolvedConfig(cfg, fullConfigPath, baseDir, inputPath, outputDir);
            resolved.Validate();
            return resolved;
        }
    }
}
