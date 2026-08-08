using extgen.Emitters.Utils;
using extgen.Models.Config;
using System.Text;

namespace extgen.Emitters.Rust
{
    internal sealed class RustEmitterContext(string ExtName, RustEmitterSettings Settings, RuntimeNaming Runtime)
    {
        public string ExtName { get; } = ExtName;
        public RustEmitterSettings Settings { get; } = Settings;
        public RuntimeNaming Runtime { get; } = Runtime;
    }

    internal static class RustCodeGen
    {
        public static string RustParamList(IEnumerable<ExportParam> ps) =>
            string.Join(", ", ps.Select(p => $"{SanitizeIdent(p.Name)}: {p.HostType.AsRustType()}"));

        public static string SanitizeIdent(string name)
        {
            if (string.IsNullOrEmpty(name)) return "arg";
            var sb = new StringBuilder();
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            var s = sb.ToString();
            if (char.IsDigit(s[0])) s = "_" + s;
            // Rust keywords
            return s switch
            {
                "type" or "ref" or "mut" or "fn" or "mod" or "use" or "priv" or "pub" or "crate" or "self" or "super" or "async" or "await" or "dyn" or "match" or "loop" or "while" or "for" or "in" or "if" or "else" or "return" or "break" or "continue" or "where" or "impl" or "trait" or "struct" or "enum" or "const" or "static" or "let" or "true" or "false"
                    => "r#" + s,
                _ => s
            };
        }

        public static string UserFnName(string functionName) => SanitizeIdent(functionName);
    }
}
