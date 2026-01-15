

namespace codegencore.Writers.Lang
{
    public readonly record struct SwiftParam(
        string External,
        string Internal,
        string Type,
        string? Default = null);

    public class SwiftWriter(ICodeWriter io) : BaseWriter<SwiftWriter>(io)
    {
        // ========================
        // Imports
        // ========================

        public SwiftWriter Import(string module) => Line($"import {module}");

        // Handy overload for @_implementationOnly import if you ever need it.
        public SwiftWriter Import(string module, bool implementationOnly)
        {
            if (implementationOnly)
                return Line($"@_implementationOnly import {module}");
            return Import(module);
        }

        // ========================
        // Types: struct / class / protocol / extension
        // ========================

        public SwiftWriter Struct(string name, IEnumerable<string>? modifiers = null, IEnumerable<string>? inher = null, Action<SwiftWriter>? body = null)
        {
            var mods = modifiers is null || !modifiers.Any()
                ? ""
                : string.Join(" ", modifiers) + " ";

            var inh = inher is null || !inher.Any()
                ? ""
                : $": {string.Join(", ", inher)}";

            Line($"{mods}struct {name}{inh}");
            Block(_ => body?.Invoke(this), trailingNewLine: true);
            return this;
        }

        // Backwards-compatible shorthand: Struct(name, inher, body)
        public SwiftWriter Struct(string name, IEnumerable<string>? inher, Action<SwiftWriter> body) => Struct(name, modifiers: null, inher: inher, body: body);

        public SwiftWriter Class(string name, IEnumerable<string>? modifiers = null, IEnumerable<string>? inher = null, Action<SwiftWriter>? body = null)
        {
            var mods = modifiers is null || !modifiers.Any()
                ? ""
                : string.Join(" ", modifiers) + " ";

            var inh = inher is null || !inher.Any()
                ? ""
                : $": {string.Join(", ", inher)}";

            Line($"{mods}class {name}{inh}");
            Block(_ => body?.Invoke(this), trailingNewLine: true);
            return this;
        }

        public SwiftWriter Class(string name, IEnumerable<string>? inher, Action<SwiftWriter> body) => Class(name, modifiers: null, inher: inher, body: body);

        public SwiftWriter Protocol(string name, IEnumerable<string>? modifiers = null, IEnumerable<string>? inher = null, Action<SwiftWriter>? body = null)
        {
            var mods = modifiers is null || !modifiers.Any()
                ? ""
                : string.Join(" ", modifiers) + " ";

            var inh = inher is null || !inher.Any()
                ? ""
                : $": {string.Join(", ", inher)}";

            Line($"{mods}protocol {name}{inh}");
            Block(_ => body?.Invoke(this), trailingNewLine: true);
            return this;
        }

        public SwiftWriter Protocol(string name, IEnumerable<string>? inher, Action<SwiftWriter> body) => Protocol(name, modifiers: null, inher: inher, body: body);

        public SwiftWriter Extension(string type, string? whereClause = null, Action<SwiftWriter>? body = null)
        {
            var wherePart = string.IsNullOrWhiteSpace(whereClause)
                ? ""
                : $" where {whereClause}";

            Line($"extension {type}{wherePart}");
            Block(_ => body?.Invoke(this), trailingNewLine: true);
            return this;
        }

        public SwiftWriter Extension(string type, Action<SwiftWriter> body) => Extension(type, whereClause: null, body: body);

        // ========================
        // Members
        // ========================

        public SwiftWriter Let(string name, string? type, string? init = null, IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null || !modifiers.Any()
                ? ""
                : string.Join(" ", modifiers) + " ";

            var line = $"{mods}let {name}";
            if (!string.IsNullOrEmpty(type))
                line += $": {type}";
            if (init is not null)
                line += $" = {init}";
            return Line(line);
        }

        public SwiftWriter Var(string name, string? type, string? init = null, IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null || !modifiers.Any()
                ? ""
                : string.Join(" ", modifiers) + " ";

            var line = $"{mods}var {name}";
            if (!string.IsNullOrEmpty(type))
                line += $": {type}";
            if (init is not null)
                line += $" = {init}";
            return Line(line);
        }

        public SwiftWriter Assign(string name, string rhs) => Line($"{name} = {rhs}");

        public SwiftWriter DeclareOrAssign(bool declare, string name, string? type, string rhs)
        {
            if (declare)
                return Let(name, type, init: rhs);
            return Assign(name, rhs);
        }

        public SwiftWriter Func(string name, IEnumerable<SwiftParam> parameters, string? returnType, IEnumerable<string>? modifiers, Action<SwiftWriter> body)
        {
            var mods = modifiers is null ? "" : $"{string.Join(" ", modifiers)} ";

            var plist = string.Join(", ", parameters.Select(p =>
            {
                // Resolve the internal name
                var internalName = string.IsNullOrEmpty(p.Internal) ? p.External : p.Internal;
                if (string.IsNullOrEmpty(internalName))
                    throw new ArgumentException("SwiftParam must have at least an internal or external name.");

                string labelPart;

                if (string.IsNullOrEmpty(p.External))
                {
                    // No external label:  "_ internal"
                    labelPart = $"_ {internalName}";
                }
                else if (p.External == internalName)
                {
                    // Same external + internal: just "name"
                    // Swift interprets this as both labels being the same.
                    labelPart = internalName;
                }
                else
                {
                    // Different external/internal: "ext internal"
                    labelPart = $"{p.External} {internalName}";
                }

                var def = p.Default is null ? "" : $" = {p.Default}";
                return $"{labelPart}: {p.Type}{def}";
            }));

            var ret = returnType is null ? "" : $" -> {returnType}";

            Line($"{mods}func {name}({plist}){ret}");
            Block(_ => body(this), trailingNewLine: true);
            return this;
        }

        // Handy overload when you don’t care about modifiers
        public SwiftWriter Func(string name, IEnumerable<SwiftParam> parameters, string? returnType, Action<SwiftWriter> body) => Func(name, parameters, returnType, modifiers: null, body);

        public SwiftWriter Init(IEnumerable<SwiftParam> parameters, IEnumerable<string>? modifiers, Action<SwiftWriter> body)
        {
            var mods = modifiers is null || !modifiers.Any()
                ? ""
                : string.Join(" ", modifiers) + " ";

            var plist = string.Join(", ", parameters.Select(p =>
            {
                var ext = string.IsNullOrEmpty(p.External) ? "_" : p.External;
                var inner = string.IsNullOrEmpty(p.Internal) ? p.External : p.Internal;
                var def = p.Default is null ? "" : $" = {p.Default}";
                return $"{ext} {inner}: {p.Type}{def}";
            }));

            Line($"{mods}init({plist})");
            Block(_ => body(this), trailingNewLine: true);
            return this;
        }

        public SwiftWriter Init(IEnumerable<SwiftParam> parameters, Action<SwiftWriter> body) => Init(parameters, modifiers: null, body);

        // ========================
        // Control flow
        // ========================

        public SwiftWriter If(string cond, Action<SwiftWriter> thenBody, Action<SwiftWriter>? elseBody = null)
        {
            Line($"if {cond}");
            Block(_ => thenBody(this));
            if (elseBody is not null)
            {
                Line("else");
                Block(_ => elseBody(this));
            }
            return this;
        }

        public SwiftWriter ForIn(string header, Action<SwiftWriter> body)
        {
            Line($"for {header}");
            Block(_ => body(this));
            return this;
        }

        public SwiftWriter While(string cond, Action<SwiftWriter> body)
        {
            Line($"while {cond}");
            Block(_ => body(this));
            return this;
        }

        public SwiftWriter Switch(string expr, Action<SwiftWriter> bodyHeaderless)
        {
            Line($"switch {expr}");
            Block(_ => bodyHeaderless(this));
            return this;
        }

        public SwiftWriter Case(string pattern, Action<SwiftWriter> body)
        {
            Line($"case {pattern}:");
            Block(_ => body(this));
            return this;
        }

        public SwiftWriter Default(Action<SwiftWriter> body)
        {
            Line("default:");
            Block(_ => body(this));
            return this;
        }

        public SwiftWriter Try(Action<SwiftWriter> tryBody, Action<SwiftWriter> catchBody)
        {
            Line("do");
            Block(_ => tryBody(this));
            Line("catch");
            Block(_ => catchBody(this));
            return this;
        }

        public SwiftWriter Guard(string condition, Action<SwiftWriter> guardBody) => Append($"guard {condition} else ").Block(guardBody, true);

        public SwiftWriter GuardCase(string pattern, string expr, Action<SwiftWriter> elseBody) => Append($"guard {pattern} = {expr} else ").Block(elseBody);

        public SwiftWriter Call(string fn, IEnumerable<(string Label, string Expr)> args)
        {
            var argList = string.Join(", ", args.Select(a => $"{a.Label}: {a.Expr}"));
            return Line($"{fn}({argList})");
        }

        public SwiftWriter Return(string? expr = null) => Line(expr is null ? "return" : $"return {expr}");

        // ========================
        // Enums (using EnumMember)
        // ========================

        public SwiftWriter Enum(string name, IEnumerable<EnumMember> members, string? rawType = null, IEnumerable<string>? modifiers = null)
        {
            var mods = modifiers is null || !modifiers.Any()
                ? ""
                : string.Join(" ", modifiers) + " ";

            var raw = rawType is null
                ? ""
                : $": {rawType}";

            Line($"{mods}enum {name}{raw}");
            Block(_ =>
            {
                foreach (var m in members)
                {
                    var line = $"case {m.Name}";

                    if (m.Value is not null)
                        line += $" = {m.Value}";

                    if (m.Comment is not null)
                        line += $" // {m.Comment}";

                    _.Line(line);
                }
            }, trailingNewLine: true);

            return this;
        }

        public SwiftWriter Enum(string name, string rawType, params EnumMember[] members) => Enum(name, members, rawType, modifiers: null);

        public SwiftWriter Enum(string name, params EnumMember[] members) => Enum(name, members, rawType: null, modifiers: null);

        // ========================
        // Comments / sections
        // ========================

        public SwiftWriter Comment(string comment)
        {
            if (string.IsNullOrEmpty(comment))
                return Line("//");
            foreach (var ln in comment.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                Line($"// {ln}");
            return this;
        }

        public SwiftWriter Section(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Line("//");
            return Comment($"""
                =========================
                {name}
                =========================
                """);
        }
    }
}
