
namespace codegencore.Writers.Lang
{
    public readonly record struct EnumMember(string Name, string? Value = null, string? Comment = null)
    {
        public string ToMemberString() =>
            Comment is null
                ? Value is null ? Name : $"{Name} = {Value}"
                : $"{Name}{(Value is null ? string.Empty : $" = {Value}")} // {Comment}";
    }

    public enum InitStyle { None, Equals, Parens, Braces }

    public class CxxWriter<TSelf>(ICodeWriter io) : CStyleWriter<TSelf>(io)
        where TSelf : CxxWriter<TSelf>
    {
        // #include / #pragams
        public TSelf PragmaOnce() => Line("#pragma once");

        public TSelf Include(string header, bool system = true)
            => Line(system ? $"#include <{header}>" : $"#include \"{header}\"");

        public TSelf IfDef(string macro, Action<TSelf> thenBody, Action<TSelf>? elseBody = null)
        {
            Line($"#ifdef {macro}");
            thenBody((TSelf)this);
            if (elseBody is not null)
            {
                Line("#else");
                elseBody((TSelf)this);
            }
            Line("#endif");
            return (TSelf)this;
        }

        // using namespace ...
        public TSelf UsingNamespace(string ns) => Line($"using namespace {ns};");

        // namespace a::b { ... }
        public TSelf Namespace(string fullyQualified, Action<TSelf> body)
        {
            Line($"namespace {fullyQualified}").Block(body, trailingNewLine: true);
            Line(); // blank after
            return (TSelf)this;
        }

        // extern "C"/"C++" expr or block
        public TSelf Extern(string linkage, Action<TSelf> expr)
            => Append($"extern \"{linkage}\" ").Block(expr);

        public TSelf ExternBlock(string linkage, Action<TSelf> body)
            => Line($"extern \"{linkage}\"").Block(body, trailingNewLine: true);

        // Declarations / assignments
        /// <summary>
        /// General-purpose C++ declaration builder, similar to Function(...).
        /// Examples:
        ///   Declare("int", "k", "42", modifiers: new[]{"static","constexpr"}, initStyle: InitStyle.Equals);
        ///   Declare("std::array<int,3>", "a", "1, 2, 3", qualifiers: null, initStyle: InitStyle.Braces);
        ///   Declare("float", "buf[16]", modifiers:new[]{"thread_local"}, attrSuffix:"alignas(64)");
        /// </summary>
        public TSelf Declare(
            string type,
            string name,
            string? initializer = null,
            IEnumerable<string>? modifiers = null,     // e.g. "static", "inline", "constexpr", "thread_local", "extern"
            IEnumerable<string>? qualifiers = null,    // trailing qualifiers, e.g. "const", "volatile"
            string? attrPrefix = null,                 // attributes before specifiers, e.g. "[[maybe_unused]]"
            string? attrSuffix = null,                 // attributes after the declarator, e.g. "alignas(64)"
            InitStyle initStyle = InitStyle.Equals,
            bool endWithSemicolon = true
        )
        {
            // Prefix attributes (e.g. [[nodiscard]])
            if (!string.IsNullOrWhiteSpace(attrPrefix))
                Append(attrPrefix).Append(" ");

            // Modifiers/specifiers (emit in caller-provided order)
            if (modifiers is not null)
            {
                var mods = string.Join(" ", modifiers.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(mods))
                    Append(mods).Append(" ");
            }

            // Type
            if (!string.IsNullOrWhiteSpace(type))
                Append(type).Append(" ");

            // Trailing qualifiers (e.g. const, volatile)
            if (qualifiers is not null)
            {
                var quals = string.Join(" ", qualifiers.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(quals))
                    Append(quals).Append(" ");
            }

            // Name
            Append(name);

            // Suffix attributes (e.g. alignas(64))
            if (!string.IsNullOrWhiteSpace(attrSuffix))
                Append(" ").Append(attrSuffix);

            // Initializer
            if (initStyle != InitStyle.None && !string.IsNullOrWhiteSpace(initializer))
            {
                switch (initStyle)
                {
                    case InitStyle.Equals: Append(" = ").Append(initializer); break;
                    case InitStyle.Parens: Append("(").Append(initializer).Append(")"); break;
                    case InitStyle.Braces: Append("{").Append(initializer).Append("}"); break;
                }
            }

            if (endWithSemicolon) Line(";");
            return (TSelf)this;
        }

        // Convenience helpers for common init styles
        public TSelf DeclareEq(string type, string name, string expr,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? qualifiers = null,
            string? attrPrefix = null,
            string? attrSuffix = null,
            bool endWithSemicolon = true)
            => Declare(type, name, expr, modifiers, qualifiers, attrPrefix, attrSuffix, InitStyle.Equals, endWithSemicolon);

        public TSelf DeclareParens(string type, string name, string args,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? qualifiers = null,
            string? attrPrefix = null,
            string? attrSuffix = null,
            bool endWithSemicolon = true)
            => Declare(type, name, args, modifiers, qualifiers, attrPrefix, attrSuffix, InitStyle.Parens, endWithSemicolon);

        public TSelf DeclareBraces(string type, string name, string elements,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? qualifiers = null,
            string? attrPrefix = null,
            string? attrSuffix = null,
            bool endWithSemicolon = true)
            => Declare(type, name, elements, modifiers, qualifiers, attrPrefix, attrSuffix, InitStyle.Braces, endWithSemicolon);


        // struct Foo { ... };
        public TSelf Struct(string name, Action<TSelf> body)
            => Line($"struct {name}").Block(body).Line(";");

        // enum Bar { ... };

        public TSelf Enum(string name, IEnumerable<EnumMember> members, string? underlying = null)
        {
            var list = members.ToList();
            var type = string.IsNullOrEmpty(underlying) ? string.Empty : $" : {underlying}";
            Line($"enum class {name}{type}")
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
              .Line(";");
            return (TSelf)this;
        }

        public TSelf Enum(string name, params (string Name, string? Value, string? Comment)[] members)
            => Enum(name, members.Select(m => new EnumMember(m.Name, m.Value, m.Comment)));

        public TSelf Enum(string name, IEnumerable<string> memberNames)
            => Enum(name, memberNames.Select(n => new EnumMember(n)));

        // { a, b, c }
        public TSelf InitList(IEnumerable<string> items)
            => Append("{").AppendJoin(items).Append("}");
    }

}

