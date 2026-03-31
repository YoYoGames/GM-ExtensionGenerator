using extgen.Models.Config;
using extgen.Planning;

namespace extgen.Config
{
    public static class ConfigResolver
    {
        /// <summary>
        /// Resolves relative paths in config to absolute paths and validates inputs.
        /// </summary>
        public static ResolvedConfig Resolve(ExtGenConfig cfg, string configPath, Func<string?, string, string> resolvePath)
        {
            // Path resolution strategy:
            // - Config file location is the "base directory" for all relative paths
            // - cfg.Input (GMIDL file) is resolved relative to config location
            // - cfg.Root (output directory) is resolved relative to config location
            // Example: if config is at "project/codegen/extgen.json" and Input is "../schema/api.gmidl",
            // the resolved path is "project/schema/api.gmidl"

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

            // Create output directory if it doesn't exist (fail early if permissions issue)
            Directory.CreateDirectory(outputDir);

            var resolved = new ResolvedConfig(cfg, fullConfigPath, baseDir, inputPath, outputDir);
            resolved.Validate();
            return resolved;
        }
    }
}
