namespace extgen.Utils
{
    public static class PathUtils 
    {
        public static string ResolvePath(this string? path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var expanded = Environment.ExpandEnvironmentVariables(
                path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

            return Path.IsPathRooted(expanded)
                ? expanded
                : Path.GetFullPath(Path.Combine(baseDir, expanded));
        }
    }
}
