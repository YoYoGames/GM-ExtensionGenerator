
namespace codegencore.Writers.Lang
{
    /// <summary>
    /// Specifies the scope of a GML variable declaration.
    /// </summary>
    public enum VariableScope { None, Local, Static }

    /// <summary>
    /// Specifies the type of accessor for GML data structures.
    /// </summary>
    public enum AccessorKind { Array, Map, List, Struct }

    /// <summary>
    /// Provides a fluent API for generating GameMaker Language (GML) source code with support for enums, functions, structs, and data structure accessors.
    /// </summary>
    public class GmlWriter(ICodeWriter io) : CStyleWriter<GmlWriter>(io)
    {
        /// <summary>
        /// Writes an enum declaration.
        /// </summary>
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

        /// <summary>
        /// Writes a repeat loop.
        /// </summary>
        public GmlWriter Repeat(string timesExpr, Action<GmlWriter> body) => Keyword("repeat", timesExpr, body);

        /// <summary>
        /// Writes a with context statement.
        /// </summary>
        public GmlWriter With(string contextExpr, Action<GmlWriter> body) => Keyword("with", contextExpr, body);

        /// <summary>
        /// Writes a do-until loop.
        /// </summary>
        public GmlWriter DoUntil(Action<GmlWriter> body, string condition) => Line("do").Block(body).Line($" until ({condition});");

        /// <summary>
        /// Writes an assignment statement with optional variable scope.
        /// </summary>
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

        /// <summary>
        /// Writes an anonymous function (method) with parameters.
        /// </summary>
        public GmlWriter Method(IEnumerable<string> paramNames, Action<GmlWriter> body) => Append("function(").AppendJoin(paramNames).Line(")").Block(body);

        /// <summary>
        /// Writes an anonymous function (method) without parameters.
        /// </summary>
        public GmlWriter Method(Action<GmlWriter> body) => Method([], body);

        /// <summary>
        /// Writes a named function declaration.
        /// </summary>
        public GmlWriter Function(string name, IEnumerable<string> parameters, Action<GmlWriter> body) => Append($"function {name}(").AppendJoin(parameters).Line(")").Block(body, trailingNewLine: true);

        /// <summary>
        /// Writes a struct constructor function.
        /// </summary>
        public GmlWriter Struct(string name, IEnumerable<string> ctorParams, Action<GmlWriter> body) => Append($"function {name}(").AppendJoin(ctorParams).Line(") constructor").Block(body, trailingNewLine: true);

        /// <summary>
        /// Writes a struct constructor function without parameters.
        /// </summary>
        public GmlWriter Struct(string name, Action<GmlWriter> body) => Struct(name, [], body);
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

        /// <summary>
        /// Writes an array literal.
        /// </summary>
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

        /// <summary>
        /// Represents a field in a GML struct literal.
        /// </summary>
        public readonly record struct StructField(string Name, string Expr);

        /// <summary>
        /// Writes a struct literal.
        /// </summary>
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

