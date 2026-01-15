using codegencore.Writers.Concrete;
using codegencore.Writers.JSDoc;
using System.Text;

namespace codegencore.Writers
{
    public interface ICodeWriter
    {
        int IndentLevel { get; }
        ICodeWriter Append(string value);
        ICodeWriter AppendLine(string value = "");
        ICodeWriter IncrementIndent();
        ICodeWriter DecrementIndent();
        ICodeWriter AppendJoin(IEnumerable<string> parts, string separator = ", ");
    }

    public static class CodeWriter
    {
        //public static ICodeWriter From(IndentedStringBuilder builder) => new StringBuilderCodeWriter(builder);
        public static ICodeWriter From(TextWriter writer, string indentUnit = "    ") => new TextCodeWriter(writer, indentUnit);
        public static ICodeWriter ToFile(string path, string indentUnit = "    ")
            => new TextCodeWriter(new StreamWriter(File.Create(path), Encoding.UTF8) { AutoFlush = true }, indentUnit);
        //public static ICodeWriter AsCodeWriter(this IndentedStringBuilder builder) => From(builder);
        public static ICodeWriter AsCodeWriter(this TextWriter writer) => From(writer);
    }

    /// <summary>Minimal base used by all language writers.</summary>
    public abstract class BaseWriter<TSelf>(ICodeWriter io) where TSelf : BaseWriter<TSelf>
    {
        protected readonly ICodeWriter _io = io ?? throw new ArgumentNullException(nameof(io));

        // Low-level helpers (fluent)
        public TSelf Append(string s) { _io.Append(s); return (TSelf)this; }
        public TSelf Line(string s = "") { _io.AppendLine(s); return (TSelf)this; }
        public TSelf Lines(string block)
        {
            foreach (var l in block.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
                _io.AppendLine(l);
            return (TSelf)this;
        }

        public TSelf Indent() { _io.IncrementIndent(); return (TSelf)this; }
        public TSelf Unindent() { _io.DecrementIndent(); return (TSelf)this; }

        public TSelf Block(Action<TSelf> body, bool trailingNewLine = false)
        {
            _io.AppendLine("{");
            _io.IncrementIndent();
            body((TSelf)this);
            _io.DecrementIndent();
            _io.Append("}");
            if (trailingNewLine) _io.AppendLine();
            return (TSelf)this;
        }

        public TSelf AppendJoin(IEnumerable<string> items, string sep = ", ") { _io.AppendJoin(items, sep); return (TSelf)this; }

        public TSelf ForEach<T>(IEnumerable<T> items, Action<TSelf, T> emit, string? separator = null)
        {
            bool first = true;
            foreach (var item in items)
            {
                if (separator is not null && !first)
                    io.Append(separator);
                emit((TSelf)this, item);
                first = false;
            }
            return (TSelf)this;
        }

        // Documentation
        public TSelf JsDoc(IJsDocSpec spec) => Line("/**").ForEach(spec.Lines, (w, line) => w.Line($" * {line}")).Line(" */");

        public TSelf JsDoc(Action<JsDocBuilder> build)
        {
            var jb = new JsDocBuilder();
            build(jb);
            return JsDoc(jb);
        }
    }
}

