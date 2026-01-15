
namespace codegencore.Writers.Lang
{
    /// A tiny DSL for Objective-C selectors.
    public readonly record struct ObjcParam(string Label, string Type, string Name);

    public class ObjcWriter(ICodeWriter io) : CxxWriter<ObjcWriter>(io)
    {
        // imports / pragmas
        public ObjcWriter Import(string header, bool system = false) => Line(system ? $"#import <{header}>" : $"#import \"{header}\"");

        // @protocol/@interface/@implementation/@end
        public ObjcWriter ForwardProtocol(string name) => Line($"@protocol {name};");

        public ObjcWriter Protocol(string name, IEnumerable<string>? inherits = null, Action<ObjcWriter>? body = null)
        {
            var inh = inherits is null ? "" : $" <{string.Join(", ", inherits)}>";
            Line($"@protocol {name}{inh}");
            body?.Invoke(this);
            Line("@end").Line();
            return this;
        }

        public ObjcWriter Interface(string name, string? baseClass = "NSObject", IEnumerable<string>? protocols = null, Action<ObjcWriter>? body = null)
        {
            var proto = protocols is null ? "" : $" <{string.Join(", ", protocols)}>";
            Line($"@interface {name} : {baseClass}{proto}");
            body?.Invoke(this);
            Line("@end").Line();
            return this;
        }

        public ObjcWriter ClassExtension(string className, Action<ObjcWriter> ivars)
        {
            Line($"@interface {className} ()");
            Block(_ => ivars(this), trailingNewLine: false);
            Line("@end").Line();
            return this;
        }

        public ObjcWriter Implementation(string name, Action<ObjcWriter> body)
        {
            Line($"@implementation {name}");
            body(this);
            Line("@end").Line();
            return this;
        }

        public ObjcWriter Category(string className, string categoryName, Action<ObjcWriter> body, IEnumerable<string>? protocols = null)
        {
            var proto = protocols is null ? "" : $" <{string.Join(", ", protocols)}>";
            Line($"@interface {className} ({categoryName}){proto}");
            Block(_ => { });
            Line("@end").Line();

            Line($"@implementation {className} ({categoryName})");
            Block(_ => body(this));
            Line("@end").Line();
            return this;
        }

        // @property
        public ObjcWriter Property(string attributes, string type, string name) => Line($"@property ({attributes}) {type} {name};");

        // ivar (inside @implementation { ... } is rare now; usually synthesize)
        public ObjcWriter IVar(string type, string name) => Line($"{type} {name};");

        // Methods

        public ObjcWriter InitMethod(Action<ObjcWriter> insideIfSelf)
        {
            Line("- (instancetype)init");
            Block(b =>
            {
                b.Line("self = [super init];");
                b.Keyword("if", "self", ifBody => 
                {
                    insideIfSelf(b);
                });
                b.Return("self");
            }, trailingNewLine: true);
            return this;
        }

        //  - / +, returnType, name+params
        public ObjcWriter Method(bool isClass, string returnType, string baseName, IReadOnlyList<ObjcParam> parts, Action<ObjcWriter> body)
        {
            var sig = BuildMethodSignature(isClass, returnType, baseName, parts);
            Line(sig);
            Block(_ => body(this), trailingNewLine: true);
            return this;
        }

        public ObjcWriter MethodDecl(bool isClass, string returnType, string baseName, IReadOnlyList<ObjcParam> parts) => Line(BuildMethodSignature(isClass, returnType, baseName, parts) + ";");

        private static string BuildMethodSignature(bool isClass, string ret, string baseName, IReadOnlyList<ObjcParam> parts)
        {
            var prefix = isClass ? "+" : "-";
            if (parts is null || parts.Count == 0)
                return $"{prefix} ({ret}){baseName}";

            var first = parts[0];
            var sig = $"{prefix} ({ret}){baseName}:({first.Type}){first.Name}";
            for (int i = 1; i < parts.Count; i++)
            {
                var p = parts[i];
                sig += $" {p.Label}:({p.Type}){p.Name}";
            }
            return sig;
        }

        // Message send: [recv sel:arg ...]
        public ObjcWriter MsgSend(string receiver, string baseName, IReadOnlyList<(string, string)> args)
        {
            if (args is null || args.Count == 0)
                return Append($"[{receiver} {baseName}]");

            var first = args[0];
            Append($"[{receiver} {baseName}:{first.Item1}");
            for (int i = 1; i < args.Count; i++)
            {
                var p = args[i];
                Append($" {p.Item1}:{p.Item2}");
            }
            return Append("]");
        }

        public ObjcWriter MsgSend(string receiver, string baseName, IReadOnlyList<ObjcParam> args)
        {
            if (args is null || args.Count == 0)
                return Append($"[{receiver} {baseName}]");

            var first = args[0];
            Append($"[{receiver} {baseName}:{first.Name}");
            for (int i = 1; i < args.Count; i++)
            {
                var p = args[i];
                Append($" {p.Label}:{p.Name}");
            }
            return Append("]");
        }

        // C/ObjC++ helpers (for your .mm files)
        public ObjcWriter ExternC(Action<ObjcWriter> body)
        {
            Line("extern \"C\"");
            Block(_ => body(this), trailingNewLine: true);
            return this;
        }
    }
}
