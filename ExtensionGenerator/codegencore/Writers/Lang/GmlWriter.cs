
namespace codegencore.Writers.Lang
{
    public enum VariableScope { None, Local, Static }
    public enum AccessorKind { Array, Map, List, Struct }

    public class GmlWriter(ICodeWriter io) : CStyleWriter<GmlWriter>(io)
    {
        public GmlWriter Enum(string name, IEnumerable<EnumMember> members)
        {
            var list = members.ToList();
            Line($"enum {name}")
              .Block(b =>
              {
                  for (int i = 0; i < list.Count; i++)
                  {
                      var line = list[i].ToMemberString();
                      if (i < list.Count - 1 && !line.EndsWith("//"))
                          b.Line(line + ",");
                      else
                          b.Line(line);
                  }
              })
              .Line();
            return this;
        }
        
        public GmlWriter Repeat(string timesExpr, Action<GmlWriter> body) => Keyword("repeat", timesExpr, body);

        public GmlWriter With(string contextExpr, Action<GmlWriter> body) => Keyword("with", contextExpr, body);

        public GmlWriter DoUntil(Action<GmlWriter> body, string condition) => Line("do").Block(body).Line($" until ({condition});");

        // Assigns
        public GmlWriter Assign(string identifier, string rhs, VariableScope scope = VariableScope.None) => Assign(lhs: w => w.Append(identifier), rhs: w => w.Append(rhs), scope);

        public GmlWriter Assign(string identifier, Action<GmlWriter> lhs, VariableScope scope = VariableScope.None) => Assign(w => w.Append(identifier), lhs, scope);

        public GmlWriter Assign(Action<GmlWriter> lhs, Action<GmlWriter> rhs, VariableScope scope = VariableScope.None)
        {
            var prefix = scope switch
            {
                VariableScope.Local => "var ",
                VariableScope.Static => "static ",
                _ => string.Empty
            };
            Append(prefix); lhs(this); Append(" = "); rhs(this); Line(";");
            return this;
        }

        // Functions/structs
        public GmlWriter Method(IEnumerable<string> paramNames, Action<GmlWriter> body) => Append("function(").AppendJoin(paramNames).Line(")").Block(body);

        public GmlWriter Method(Action<GmlWriter> body) => Method([], body);

        public GmlWriter Function(string name, IEnumerable<string> parameters, Action<GmlWriter> body) => Append($"function {name}(").AppendJoin(parameters).Line(")").Block(body, trailingNewLine: true);

        public GmlWriter Struct(string name, IEnumerable<string> ctorParams, Action<GmlWriter> body) => Append($"function {name}(").AppendJoin(ctorParams).Line(") constructor").Block(body, trailingNewLine: true);

        public GmlWriter Struct(string name, Action<GmlWriter> body) => Struct(name, [], body);

        // Accessors
        private static string Sig(AccessorKind k) => k switch
        {
            AccessorKind.Map => "?",
            AccessorKind.List => "!",
            AccessorKind.Struct => "$",
            _ => string.Empty
        };

        public GmlWriter Access(string identifier, AccessorKind kind, Action<GmlWriter> exprBuilder)
        {
            Append(identifier).Append("[").Append(Sig(kind));
            if (kind is not AccessorKind.Array) Append(" ");
            exprBuilder(this);
            Append("]");
            return this;
        }

        public GmlWriter Access(string identifier, AccessorKind kind, string exprLiteral) => Access(identifier, kind, w => w.Append(exprLiteral));

        // Array literals
        public GmlWriter ArrayLiteral(IEnumerable<string> elements, bool multiline = false, bool trailingNewLine = false)
        {
            var list = elements?.ToList() ?? [];

            if (!multiline || list.Count == 0)
            {
                Append("[");
                if (list.Count > 0) AppendJoin(list);
                Append("]");
                if (trailingNewLine) Line();
                return this;
            }

            // Multiline
            Line("[");
            Indent();
            for (int i = 0; i < list.Count; i++)
            {
                Line($"{list[i]}{(i < list.Count - 1 ? "," : "")}");
            }
            Unindent();

            if (trailingNewLine) Line("]");
            else Append("]");

            return this;
        }

        public GmlWriter ArrayLiteral(bool multiline = false, bool trailingNewLine = false, params string[] elements)
            => ArrayLiteral(elements, multiline, trailingNewLine);

        public GmlWriter ArrayLiteral<T>(IEnumerable<T> items, Func<T, string> toExpr, bool multiline = false, bool trailingNewLine = false)
            => ArrayLiteral(items?.Select(toExpr) ?? [], multiline, trailingNewLine);

        // Struct literals
        public readonly record struct StructField(string Name, string Expr);

        public GmlWriter StructLiteral(IEnumerable<StructField> fields, bool multiline = false, bool trailingNewLine = false)
        {
            var list = fields?.ToList() ?? [];

            if (!multiline || list.Count == 0)
            {
                Append("{ ");
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    Append(f.Name).Append(": ").Append(f.Expr);
                    if (i < list.Count - 1) Append(", ");
                }
                Append(" }");
                if (trailingNewLine) Line();
                return this;
            }

            // Multiline
            Line("{");
            Indent();
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i];
                Line($"{f.Name}: {f.Expr}{(i < list.Count - 1 ? "," : "")}");
            }
            Unindent();

            if (trailingNewLine) Line("}");
            else Append("}");

            return this;
        }

        public GmlWriter StructLiteral(bool multiline = false, bool trailingNewLine = false, params StructField[] fields)
            => StructLiteral(fields, multiline, trailingNewLine);

        public GmlWriter StructLiteral(IReadOnlyDictionary<string, string> map, bool multiline = false, bool trailingNewLine = false)
            => StructLiteral(map.Select(kv => new StructField(kv.Key, kv.Value)), multiline, trailingNewLine);

    }
}

