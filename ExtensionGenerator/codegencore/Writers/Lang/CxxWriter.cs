
namespace codegencore.Writers.Lang
{
    /// <summary>
    /// Represents a member of a C++ enum with optional value and comment.
    /// </summary>
    public readonly record struct EnumMember(string Name, string? Value = null, string? Comment = null)
    {
        public string ToMemberString() =>
            Comment is null
                ? Value is null ? Name : $"{Name} = {Value}"
                : $"{Name}{(Value is null ? string.Empty : $" = {Value}")} // {Comment}";
    }

    /// <summary>
    /// Specifies the initialization style for C++ variable declarations.
    /// </summary>
    public enum InitStyle { None, Equals, Parens, Braces }

    /// <summary>
    /// Provides a fluent API for generating C++ and C source code with support for namespaces, extern linkage, structs, enums, and advanced declarations.
    /// </summary>
    public class CxxWriter<TSelf>(ICodeWriter io) : CStyleWriter<TSelf>(io)
        where TSelf : CxxWriter<TSelf>
    {
        /// <summary>
        /// Writes a pragma once directive.
        /// </summary>
        public TSelf PragmaOnce() => Line("#pragma once");

        /// <summary>
        /// Writes an include directive.
        /// </summary>
        public TSelf Include(string header, bool system = true)
            => Line(system ? $"#include <{header}>" : $"#include \"{header}\"");

        /// <summary>
        /// Writes an ifdef conditional with optional else block.
        /// </summary>
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

        /// <summary>
        /// Writes a using namespace directive.
        /// </summary>
        public TSelf UsingNamespace(string ns) => Line($"using namespace {ns};");

        /// <summary>
        /// Writes a namespace declaration.
        /// </summary>
        public TSelf Namespace(string fullyQualified, Action<TSelf> body)
        {
            Line($"namespace {fullyQualified}").Block(body, trailingNewLine: true);
            Line();
            return (TSelf)this;
        }

        /// <summary>
        /// Writes an extern linkage expression.
        /// </summary>
        public TSelf Extern(string linkage, Action<TSelf> expr)
            => Append($"extern \"{linkage}\" ").Block(expr);

        /// <summary>
        /// Writes an extern linkage block.
        /// </summary>
        public TSelf ExternBlock(string linkage, Action<TSelf> body)
            => Line($"extern \"{linkage}\"").Block(body, trailingNewLine: true);

        /// <summary>
        /// Writes a general-purpose C++ variable declaration with optional modifiers, qualifiers, attributes, and initialization.
        /// </summary>
        public TSelf Declare(
            string type,
            string name,
            string? initializer = null,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? qualifiers = null,
            string? attrPrefix = null,
            string? attrSuffix = null,
            InitStyle initStyle = InitStyle.Equals,
            bool endWithSemicolon = true
        )
        {
            if (!string.IsNullOrWhiteSpace(attrPrefix))
                Append(attrPrefix).Append(" ");

            if (modifiers is not null)
            {
                var mods = string.Join(" ", modifiers.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(mods))
                    Append(mods).Append(" ");
            }

            if (!string.IsNullOrWhiteSpace(type))
                Append(type).Append(" ");

            if (qualifiers is not null)
            {
                var quals = string.Join(" ", qualifiers.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(quals))
                    Append(quals).Append(" ");
            }

            Append(name);

            if (!string.IsNullOrWhiteSpace(attrSuffix))
                Append(" ").Append(attrSuffix);

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

        /// <summary>
        /// Writes a declaration with equals-style initialization.
        /// </summary>
        public TSelf DeclareEq(string type, string name, string expr,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? qualifiers = null,
            string? attrPrefix = null,
            string? attrSuffix = null,
            bool endWithSemicolon = true)
            => Declare(type, name, expr, modifiers, qualifiers, attrPrefix, attrSuffix, InitStyle.Equals, endWithSemicolon);

        /// <summary>
        /// Writes a declaration with parentheses-style initialization.
        /// </summary>
        public TSelf DeclareParens(string type, string name, string args,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? qualifiers = null,
            string? attrPrefix = null,
            string? attrSuffix = null,
            bool endWithSemicolon = true)
            => Declare(type, name, args, modifiers, qualifiers, attrPrefix, attrSuffix, InitStyle.Parens, endWithSemicolon);

        /// <summary>
        /// Writes a declaration with brace-style initialization.
        /// </summary>
        public TSelf DeclareBraces(string type, string name, string elements,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? qualifiers = null,
            string? attrPrefix = null,
            string? attrSuffix = null,
            bool endWithSemicolon = true)
            => Declare(type, name, elements, modifiers, qualifiers, attrPrefix, attrSuffix, InitStyle.Braces, endWithSemicolon);

        /// <summary>
        /// Writes a struct declaration.
        /// </summary>
        public TSelf Struct(string name, Action<TSelf> body)
            => Line($"struct {name}").Block(body).Line(";");

        /// <summary>
        /// Writes an enum class declaration with optional underlying type.
        /// </summary>
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

        /// <summary>
        /// Writes a brace-enclosed initializer list.
        /// </summary>
        public TSelf InitList(IEnumerable<string> items)
            => Append("{").AppendJoin(items).Append("}");
    }

}

