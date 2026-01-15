
namespace codegencore.Writers.Concrete
{
    public sealed class TextCodeWriter(TextWriter writer, string indentUnit = "    ") : ICodeWriter, IDisposable
    {
        private readonly TextWriter _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        private readonly string _indentUnit = indentUnit;
        private int _level;
        private bool _lineStart = true;

        public int IndentLevel => _level;

        private void WriteIndentIfNeeded()
        {
            if (_lineStart)
            {
                _writer.Write(new string(' ', _level * _indentUnit.Length));
                _lineStart = false;
            }
        }
        public ICodeWriter Append(string value)
        {
            WriteIndentIfNeeded();
            _writer.Write(value);
            return this;
        }
        public ICodeWriter AppendLine(string value = "")
        {
            if (value.Length > 0) Append(value);
            _writer.WriteLine();
            _lineStart = true;
            return this;
        }
        public ICodeWriter IncrementIndent() { _level++; return this; }
        public ICodeWriter DecrementIndent() { if (_level > 0) _level--; return this; }
        public ICodeWriter AppendJoin(IEnumerable<string> parts, string separator = ", ") => Append(string.Join(separator, parts));
        public void Dispose() => _writer.Dispose();
    }

}

