namespace codegencore.Writers.JSDoc
{
    /// <summary>
    /// Builder‑style API for assembling JSDoc blocks fluently.
    /// Call via <c>io.JsDoc(doc =&gt; doc.Summary("...").Param("x", "value"))</c>.
    /// </summary>
    public readonly record struct JsDocBuilder() : IJsDocSpec
    {
        private readonly List<string> _lines = [];
        private readonly List<ParamDoc> _params = [];

        /// <summary>Adds a free‑form line to the block.</summary>
        public JsDocBuilder Line(string text)
        {
            var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
            foreach (var line in lines)
            {
                _lines.Add(line);
            }
            return this;
        }
        /// <summary>Shorthand for the first summary line.</summary>
        public JsDocBuilder Summary(string text) => Line(text);
        /// <summary>Adds a <c>@param</c> tag.</summary>
        public JsDocBuilder Param(ParamDoc p)
        {
            var typePart = p.Type is null ? string.Empty : $"{{{p.Type}}} ";
            var namePart = p.Optional ? $"[{p.Name}]" : p.Name;
            var descPart = p.Description ?? string.Empty;
            Line($"@param {typePart}{namePart} {descPart}".TrimEnd());
            _params.Add(p);
            return this;
        }
        public JsDocBuilder Member(ParamDoc p)
        {
            var typePart = p.Type is null ? string.Empty : $"{{{p.Type}}} ";
            var namePart = p.Optional ? $"[{p.Name}]" : p.Name;
            var descPart = p.Description ?? string.Empty;
            Line($"@member {typePart}{namePart} {descPart}".TrimEnd());
            _params.Add(p);
            return this;
        }

        /// <summary>Adds a <c>@returns</c> tag.</summary>
        public JsDocBuilder Returns(string type, string? description = null)
        {
            Line($"@returns {{{type}}} {description}");
            return this;
        }
        /// <summary>Adds an arbitrary raw tag, e.g., <c>@deprecated</c>.</summary>
        public JsDocBuilder Tag(string raw, string? meta = null) => Line($"@{raw} {meta}");

        public IReadOnlyList<string> Lines => _lines;
        public IReadOnlyList<ParamDoc> Params => _params;

        /// <summary>Freeze current state into an immutable spec.</summary>
        public IJsDocSpec Build() => new JsDocSpec([.. _lines], [.. _params]);
    }
}

