
namespace codegencore.Writers.Lang
{
    public class KotlinWriter(ICodeWriter io) : CStyleWriter<KotlinWriter>(io)
    {
        public KotlinWriter Package(string name) => Line($"package {name}");
        public KotlinWriter Import(string import) => Line($"import {import}");

        public KotlinWriter Interface(string name, Action<KotlinWriter> body, IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            Append($"{mods}interface {name} ").Block(_ => body(this));
            return this;
        }

        public KotlinWriter Class(
            string name,
            string? baseType,
            IEnumerable<string>? interfaces,
            Action<KotlinWriter> body,
            IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";

            var colonParts = new List<string>();
            if (!string.IsNullOrEmpty(baseType))
                colonParts.Add(baseType);
            if (interfaces is not null)
                colonParts.AddRange(interfaces);

            var suffix = colonParts.Count > 0 ? $" : {string.Join(", ", colonParts)}" : string.Empty;

            Append($"{mods}class {name}{suffix} ")
                .Block(_ => body(this));
            return this;
        }

        /// <summary>
        /// Simple function declaration (signature only).
        /// </summary>
        public KotlinWriter FunDecl(string name, IEnumerable<(string Name, string Type)> parameters, string returnType)
        {
            var plist = string.Join(", ", parameters.Select(p => $"{p.Name}: {p.Type}"));
            if (string.IsNullOrEmpty(plist))
                return Line($"fun {name}(): {returnType}");
            return Line($"fun {name}({plist}): {returnType}");
        }

        /// <summary>
        /// Function with body, e.g.:
        ///   fun foo(x: Int): Double { ... }
        /// </summary>
        public KotlinWriter Fun(
            string name,
            IEnumerable<(string Name, string Type)> parameters,
            Action<KotlinWriter> body,
            string returnType = "Unit",
            IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var plist = string.Join(", ", parameters.Select(p => $"{p.Name}: {p.Type}"));

            if (string.IsNullOrEmpty(plist))
                Line($"{mods}fun {name}(): {returnType}")
                    .Block(_ => body(this), trailingNewLine: true);
            else
                Line($"{mods}fun {name}({plist}): {returnType}")
                    .Block(_ => body(this), trailingNewLine: true);

            return this;
        }

        /// <summary>
        /// Property declaration, e.g.:
        ///   private val foo: Int
        ///   private val foo: Int = 42
        /// </summary>
        public KotlinWriter Property(
            string name,
            string type,
            string? initializer = null,
            bool mutable = false,
            IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";
            var kind = mutable ? "var" : "val";

            if (initializer is null)
                return Line($"{mods}{kind} {name}: {type}");

            return Line($"{mods}{kind} {name}: {type} = {initializer}");
        }
    }
}
