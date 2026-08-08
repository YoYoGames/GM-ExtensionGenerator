namespace extgen.Emitters.Rust
{
    public sealed class RustEmitterSettings
    {
        public bool EmitAndroidJniModule { get; init; }
        public string CrateName { get; init; } = "extension";

        public static RustEmitterSettings From(Planning.EmitterPlan plan) => new()
        {
            EmitAndroidJniModule = plan.AndroidJni,
            CrateName = SanitizeCrateName(plan.Runtime.ExtensionName)
        };

        public static string SanitizeCrateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "extension";

            var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
            var s = new string(chars).Trim('_');
            if (s.Length == 0 || char.IsDigit(s[0]))
                s = "ext_" + s;
            return s;
        }
    }
}
