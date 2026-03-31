namespace extgen.Utils
{
    /// <summary>
    /// Utilities for path resolution and manipulation.
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// Resolves a path relative to a base directory.
        /// Expands environment variables and tilde (~) to user home directory.
        /// </summary>
        /// <param name="path">Path to resolve (may be relative or absolute).</param>
        /// <param name="baseDir">Base directory for relative paths.</param>
        /// <returns>Fully resolved absolute path, or empty string if input is null/whitespace.</returns>
        public static string ResolvePath(this string? path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var expanded = Environment.ExpandEnvironmentVariables(
                path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(baseDir, expanded));
        }
    }
}
