
namespace codegencore.Writers.Lang
{
    public class CStyleWriter<TSelf>(ICodeWriter io) : BaseWriter<TSelf>(io) where TSelf : CStyleWriter<TSelf>
    {
        // keyword (expr) { … }
        public TSelf Keyword(string keyword, string parenExpr, Action<TSelf> body)
            => Line($"{keyword} ({parenExpr})").Block(body).Line();

        public TSelf If(string condition, Action<TSelf> thenBody, Action<TSelf>? elseBody = null)
        {
            Keyword("if", condition, thenBody);
            if (elseBody is not null) Line("else").Block(elseBody, true);
            return (TSelf)this;
        }

        public TSelf For(string init, string cond, string step, Action<TSelf> body)
            => Keyword("for", $"{init}; {cond}; {step}", body);

        public TSelf While(string cond, Action<TSelf> body)
            => Keyword("while", cond, body);

        public TSelf DoWhile(Action<TSelf> body, string cond)
            => Line("do").Block(body).Line($" while ({cond});");

        // functions
        public TSelf Function(string name, IEnumerable<Param> parameters, Action<TSelf> body, string? returnType = null, IEnumerable<string>? qualifiers = null, IEnumerable<string>? modifiers = null)
        {
            var prefix = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var suffix = qualifiers is null ? "" : $" {string.Join(" ", qualifiers)}";
            var plist = string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));
            Line($"{prefix}{returnType ?? "void"} {name}({plist}){suffix}")
                .Block(_ => body((TSelf)this), trailingNewLine: true);
            return (TSelf)this;
        }

        public TSelf FunctionDecl(string name, IEnumerable<Param> parameters, string? returnType = null, IEnumerable<string>? qualifiers = null, IEnumerable<string>? modifiers = null)
        {
            var prefix = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var suffix = qualifiers is null ? "" : $" {string.Join(" ", qualifiers)}";
            var plist = string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));
            Line($"{prefix}{returnType ?? "void"} {name}({plist}){suffix};");
            return (TSelf)this;
        }

        // switch/case
        public TSelf Switch(string expr, Action<SwitchBuilder> build)
        {
            Line($"switch ({expr})");
            Block(_ => build(new SwitchBuilder((TSelf)this)));
            Line();
            return (TSelf)this;
        }

        public readonly struct SwitchBuilder
        {
            private readonly TSelf _w;
            internal SwitchBuilder(TSelf w) => _w = w;

            public SwitchBuilder Case(string label, Action<TSelf> body, bool addBreak = true)
            {
                _w.Line($"case {label}:").Indent();
                body(_w);
                if (addBreak) _w.Line("break;");
                _w.Unindent();
                return this;
            }

            public SwitchBuilder Case(IEnumerable<string> labels, Action<TSelf> body, bool addBreak = true)
            {
                foreach (var lab in labels) _w.Line($"case {lab}:");
                _w.Indent();
                body(_w);
                if (addBreak) _w.Line("break;");
                _w.Unindent();
                return this;
            }

            public SwitchBuilder Default(Action<TSelf> body, bool addBreak = true)
            {
                _w.Line("default:").Indent();
                body(_w);
                if (addBreak) _w.Line("break;");
                _w.Unindent();
                return this;
            }
        }

        // assigns
        public TSelf Assign(string ident, string rhs, string? type = null)
        {
            if (!string.IsNullOrEmpty(type)) Append($"{type} ");
            Append(ident).Append(" = ").Append(rhs).Line(";");
            return (TSelf)this;
        }

        public TSelf Assign(Action<TSelf> lhs, Action<TSelf> rhs, string? type = null)
        {
            var prefix = !string.IsNullOrEmpty(type) ? $"{type} " : string.Empty;
            Append(prefix); lhs((TSelf)this); Append(" = "); rhs((TSelf)this); Line(";");
            return (TSelf)this;
        }

        public TSelf Assign(string identifier, Action<TSelf> lhs, string? type = null) => Assign(w => w.Append(identifier), lhs, type);

        // calls
        public TSelf Call(string fn, params string[] args) => Append(fn).Append("(").AppendJoin(args).Append(")");

        // return
        public TSelf Return(string? expr = null) => Line(expr is null ? "return;" : $"return {expr};");

        public TSelf Return(Action<TSelf> expr) {
            Append("return ");
            expr((TSelf)this);
            Line(";");
            return (TSelf)this;
        }

        // comments
        public TSelf Comment(string comment)
        {
            if (string.IsNullOrEmpty(comment)) return Line("//");
            foreach (var ln in comment.Split(["\r\n", "\n"], StringSplitOptions.None))
                Line($"// {ln}");
            return (TSelf)this;
        }

        public TSelf Section(string name)
        {
            if (string.IsNullOrEmpty(name)) return Line("//");
            return Comment($"""
                #####################################################################
                # {name}
                #####################################################################
                """);
        }
    }
}

