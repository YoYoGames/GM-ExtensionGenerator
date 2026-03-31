
namespace codegencore.Writers.Lang
{
    /// <summary>
    /// Represents a member of a Java enum with optional constructor arguments and comments.
    /// </summary>
    public readonly record struct JavaEnumMember(string Name, string? CtorArg = null, string? Comment = null, string? Type = null)
    {
        public string ToMemberString()
        {
            var typeCast = string.IsNullOrEmpty(Type) ? string.Empty : $"({Type})";
            var core = CtorArg is null ? Name : $"{Name}({typeCast}{CtorArg})";
            if (!string.IsNullOrEmpty(Comment))
                core += $" // {Comment}";
            return core;
        }
    }

    /// <summary>
    /// Represents a Java annotation with optional arguments.
    /// </summary>
    public readonly record struct JavaAnnotation(string Name, string? Args = null)
    {
        public override string ToString() => Args is null ? $"@{Name}" : $"@{Name}({Args})";
    }

    /// <summary>
    /// Provides a fluent API for generating Java source code with support for classes, interfaces, enums, records, and annotations.
    /// </summary>
    public class JavaWriter(ICodeWriter io) : CStyleWriter<JavaWriter>(io)
    {
        /// <summary>
        /// Writes a package declaration.
        /// </summary>
        public JavaWriter Package(string packageName) => Line($"package {packageName};");

        /// <summary>
        /// Writes an import statement.
        /// </summary>
        public JavaWriter Import(string import) => Line($"import {import};");

        /// <summary>
        /// Writes a class declaration with optional modifiers.
        /// </summary>
        public JavaWriter Class(string name, Action<JavaWriter> body, IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            Append($"{mods}class {name} ").Block(_ => body(this));
            return this;
        }

        public JavaWriter Class(
            string name,
            string extends,
            Action<JavaWriter> body,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? implements = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var impl = implements is null ? "" : $"implements {string.Join(", ", implements)} ";
            Append($"{mods}class {name} extends {extends} {impl}")
                .Block(_ => body(this));
            return this;
        }

        /// <summary>
        /// Writes an interface declaration with optional modifiers.
        /// </summary>
        public JavaWriter Interface(string name, Action<JavaWriter> body, IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            Append($"{mods}interface {name} ")
                .Block(_ => body(this));
            return this;
        }

        public JavaWriter ModBlock(IEnumerable<string>? modifiers, Action<JavaWriter> body)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            Append(mods).Block(_ => body(this));
            return this;
        }

        /// <summary>
        /// Writes a field declaration with optional initializer and modifiers.
        /// </summary>
        public JavaWriter Field(
            string type,
            string name,
            string? initializer = null,
            IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            if (initializer is null)
                return Line($"{mods}{type} {name};");
            return Line($"{mods}{type} {name} = {initializer};");
        }

        /// <summary>
        /// Writes a constructor declaration with parameters and body.
        /// </summary>
        public JavaWriter Constructor(
            string name,
            IEnumerable<Param> parameters,
            Action<JavaWriter> body,
            IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var plist = string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));
            Line($"{mods}{name}({plist})")
                .Block(_ => body(this), trailingNewLine: true);
            return this;
        }

        /// <summary>
        /// Writes an enum declaration with members and optional body.
        /// </summary>
        public JavaWriter Enum(
            string name,
            IEnumerable<JavaEnumMember> members,
            Action<JavaWriter>? body = null,
            IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var list = members.ToList();

            Line($"{mods}enum {name}")
                .Block(b =>
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var line = list[i].ToMemberString();
                        if (i < list.Count - 1 && !line.EndsWith("//"))
                            b.Line(line + ",");
                        else if (i == list.Count - 1 && !line.EndsWith("//"))
                            b.Line(line + ";");
                        else
                            b.Line(line);
                    }

                    if (body != null)
                    {
                        b.Line();
                        body(b);
                    }
                });

            return this;
        }

        /// <summary>
        /// Writes a Java record declaration with optional body and implements clause.
        /// Example: <c>public record Point(int x, int y) implements Serializable { ... }</c>
        /// </summary>
        /// <param name="name">Record name (optionally with type params, e.g. "Pair&lt;T,U&gt;").</param>
        /// <param name="components">Record components (type and name).</param>
        /// <param name="body">Optional body block writer; if null, writes empty braces.</param>
        /// <param name="modifiers">Modifiers such as "public" or "sealed".</param>
        /// <param name="implements">Interfaces to implement, e.g., "Serializable" or "Comparable&lt;Point&gt;".</param>
        public JavaWriter Record(
            string name,
            IEnumerable<Param> components,
            Action<JavaWriter>? body = null,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? implements = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var compList = string.Join(", ", components.Select(c => $"{c.Type} {c.Name}"));
            var impl = implements is null ? "" : $" implements {string.Join(", ", implements)}";

            Line($"{mods}record {name}({compList}){impl}")
                .Block(b => body?.Invoke(b), trailingNewLine: true);

            return this;
        }

        public JavaWriter Record(
            string name,
            IEnumerable<Param> components,
            IEnumerable<string>? modifiers)
            => Record(name, components, body: null, modifiers: modifiers, implements: null);

        public JavaWriter Record(
            string name,
            IEnumerable<Param> components,
            Action<JavaWriter> body)
            => Record(name, components, body, modifiers: null, implements: null);

        public JavaWriter Record(
            string name,
            IEnumerable<Param> components)
            => Record(name, components, body: null, modifiers: null, implements: null);

        /// <summary>
        /// Writes a compact constructor inside a record body.
        /// </summary>
        public JavaWriter RecordCompactCtor(
            string recordName,
            IEnumerable<Param> components,
            Action<JavaWriter> body,
            IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var plist = string.Join(", ", components.Select(p => $"{p.Type} {p.Name}"));
            Line($"{mods}{recordName}({plist})")
                .Block(_ => body(this), trailingNewLine: true);
            return this;
        }

        /// <summary>
        /// Writes annotations one per line above the declaration.
        /// </summary>
        public JavaWriter Annotations(IEnumerable<string>? annotations)
        {
            if (annotations is null) return this;
            foreach (var a in annotations)
                Line(a.StartsWith("@", StringComparison.Ordinal) ? a : $"@{a}");
            return this;
        }

        public JavaWriter Annotations(IEnumerable<JavaAnnotation>? annotations)
            => Annotations(annotations?.Select(a => a.ToString()));

        /// <summary>
        /// Field with annotations/modifiers and optional initializer.
        /// Example: <c>@Deprecated private static final int COUNT = 3;</c>
        /// </summary>
        public JavaWriter Field(
            string type,
            string name,
            string? initializer = null,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? annotations = null,
            bool endWithSemicolon = true)
        {
            Annotations(annotations);
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            if (initializer is null)
                Line($"{mods}{type} {name}{(endWithSemicolon ? ";" : "")}");
            else
                Line($"{mods}{type} {name} = {initializer}{(endWithSemicolon ? ";" : "")}");
            return this;
        }

        /// <summary>
        /// Multiple declarators sharing the same type/modifiers/annotations.
        /// Example: <c>private int a = 1, b, c = foo();</c>
        /// </summary>
        public JavaWriter Fields(
            string type,
            IEnumerable<(string Name, string? Init)> declarators,
            IEnumerable<string>? modifiers = null,
            IEnumerable<string>? annotations = null,
            bool endWithSemicolon = true)
        {
            var list = declarators?.ToList() ?? [];
            if (list.Count == 0) return this;

            Annotations(annotations);
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            Append($"{mods}{type} ");

            for (int i = 0; i < list.Count; i++)
            {
                var (n, init) = list[i];
                if (init is null) Append(n);
                else Append($"{n} = {init}");
                if (i < list.Count - 1) Append(", ");
            }
            if (endWithSemicolon) Line(";"); else Line();
            return this;
        }

        /// <summary>
        /// Convenience for <c>public static final</c> constants.
        /// </summary>
        public JavaWriter Const(
            string type,
            string name,
            string valueExpr,
            IEnumerable<string>? annotations = null,
            IEnumerable<string>? extraModifiers = null)
        {
            var mods = new List<string> { "public", "static", "final" };
            if (extraModifiers is not null) mods.AddRange(extraModifiers);
            return Field(type, name, valueExpr, mods, annotations);
        }
    }
}
