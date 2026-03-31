using codegencore.Models;
using extgen.Models;
using gmidlreader;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace extgen.Parsing.Gmidl
{
    internal enum IrSymbolKind { Enum, Struct }

    internal sealed record IrSymbol(IrSymbolKind Kind, string Name);

    internal partial class GmidlSchemaParser(GMIDLDatabase db)
    {
        private readonly GMIDLDatabase _db = db;

        private static readonly Regex _matchCollection = MatchCollection();
        private static readonly Regex _matchOptional = MatchOptional();

        // Instance state (no statics; safer and deterministic)
        private ImmutableArray<IrEnum> _enums = [];
        private ImmutableArray<IrStruct> _structs = [];
        private ImmutableArray<IrConstant> _constants = [];
        private ImmutableArray<IrFunction> _functions = [];

        private Dictionary<string, IrSymbolKind> _typeSymbols = new(StringComparer.Ordinal);

        // Hint → primitive mapping (kept from your original idea)
        private readonly Dictionary<string, GMIDLPrimitive> _hintToPrimitive = new(StringComparer.OrdinalIgnoreCase)
        {
            ["double"] = GMIDLPrimitive.Double,
            ["real"] = GMIDLPrimitive.Double,
            ["float"] = GMIDLPrimitive.Float,

            ["int"] = GMIDLPrimitive.Int32,
            ["int32"] = GMIDLPrimitive.Int32,
            ["uint"] = GMIDLPrimitive.UInt32,
            ["uint32"] = GMIDLPrimitive.UInt32,

            ["int64"] = GMIDLPrimitive.Int64,

            ["bool"] = GMIDLPrimitive.Bool,
            ["string"] = GMIDLPrimitive.String,
            ["cstring"] = GMIDLPrimitive.CString,

            ["object"] = GMIDLPrimitive.Object,
            ["array"] = GMIDLPrimitive.Array,
            ["any"] = GMIDLPrimitive.GMVal,

            ["func"] = GMIDLPrimitive.Function,
            ["function"] = GMIDLPrimitive.Function,

            // not always present in GMIDLPrimitive depending on your reader,
            // but kept here as hints:
            ["buffer"] = GMIDLPrimitive.Pointer, // we'll special-case to IrType.Buffer
        };

        private static readonly Dictionary<string, IrType> _hintToIrBuiltin = new(StringComparer.OrdinalIgnoreCase)
        {
            // primitives missing from GMIDLPrimitive
            ["int8"] = IrType.Int8,
            ["uint8"] = IrType.UInt8,
            ["int16"] = IrType.Int16,
            ["uint16"] = IrType.UInt16,
            ["uint64"] = IrType.UInt64,

            // non-primitive builtins
            ["buffer"] = IrType.Buffer,
        };

        public IrCompilation Build()
        {
            if (_db.Modules.Count == 0)
                return new IrCompilation("", [], [], [], []);

            var module = _db.Modules[0];

            // Build symbol table first (so type parsing can resolve Named)
            _typeSymbols = new(StringComparer.Ordinal);

            foreach (var e in module.Data.Enums)
                AddSymbol(e.Name, IrSymbolKind.Enum);

            foreach (var c in module.Data.Classes)
                AddSymbol(StripClassName(c.Name), IrSymbolKind.Struct);

            void AddSymbol(string name, IrSymbolKind kind)
            {
                if (_typeSymbols.TryGetValue(name, out var existing) && existing != kind)
                    throw new InvalidOperationException(
                        $"Type name collision: '{name}' is both {existing} and {kind}.");
                _typeSymbols[name] = kind;
            }

            // Parse
            _enums = [.. module.Data.Enums.Select(ParseEnum)];
            _structs = [.. module.Data.Classes.Select(ParseStruct)];
            _constants = [.. module.Data.Constants.Select(ParseConstant)];
            _functions = [.. module.Data.Functions.Select(f => ParseFunction(f, null))];

            // Sort structs so deps come first
            var sortedStructs = TopologicallySortStructs(_structs);

            return new IrCompilation(module.Name, _enums, sortedStructs, _functions, _constants);
        }

        private static string StripClassName(string gmidlClassName)
        {
            // Your original: cls.Name[2..^2]
            // Keep it but guard for short names.
            if (gmidlClassName.Length >= 4)
                return gmidlClassName[2..^2];
            return gmidlClassName;
        }

        private IrConstant ParseConstant(GMIDLNode<GMIDLConstant> cst)
        {
            var primitiveType = cst.Data.Type.PrimitiveOrNull()
                ?? throw new InvalidOperationException("Constants need to have a defined type");

            return new IrConstant(
                cst.Name,
                ParseType(primitiveType, cst.Attributes),
                cst.Data.Value ?? "");
        }

        private IrEnum ParseEnum(GMIDLNode<GMIDLEnum> enm)
        {
            // default type if unspecified
            var primitiveType = enm.Data.DefaultType.PrimitiveOrNull() ?? GMIDLPrimitive.Int32;

            // IMPORTANT:
            // In the new world, enum fields in IR should be IrType.Named(Enum, name)
            // The enum definition itself still carries its underlying IrType (used for native codegen).
            var underlying = ParseType(primitiveType, enm.Attributes);

            return new IrEnum(
                enm.Name,
                underlying,
                [.. enm.Data.Elements.Select(ParseEnumMember)]);
        }

        private static IrEnumMember ParseEnumMember(GMIDLNode<GMIDLEnumElement> enm)
            => new(enm.Name, enm.Data.Value);

        private IrStruct ParseStruct(GMIDLNode<GMIDLClass> cls)
        {
            var name = StripClassName(cls.Name);
            var classType = new IrType.Named(NamedKind.Struct, name);

            var hidden = cls.Attributes.Enabled("hidden");
                
            var fields =
                cls.Data.Properties
                   .Where(p => p.Attributes.Enabled("field"))
                   .Select(ParseField)
                   .ToImmutableArray();

            var functions = cls.Data.Functions
                //.Where(p => p.Attributes.Enabled("method"))
                .Select(f => ParseFunction(f, classType))
                .ToImmutableArray();

            return new IrStruct(name, fields, functions, Hidden: hidden);
        }

        private IrField ParseField(GMIDLNode<GMIDLProperty> f)
        {
            var attributes = f.Attributes;

            var value = attributes.GetAsString("value");

            var hidden = f.Attributes.Enabled("hidden");

            bool isOptional = bool.TryParse(attributes.GetAsString("optional") ?? "false", out var opt) && opt;

            // Some GMIDL shapes already mark optional on the node; keep attribute too
            if (isOptional) attributes.Enable("optional");

            var type = ParseType(f.Data.Type, attributes);

            return new IrField(
                Name: f.Name,
                Type: type,
                DefaultLiteral: value,
                Required: !isOptional,
                Value: value, 
                Hidden: hidden);
        }

        private IrFunction ParseFunction(GMIDLNode<GMIDLFunction> func, IrType.Named? cls)
        {
            var selfParam = (cls is not null && func.Data.SpecialArgs.Any(arg => arg.Name == "self"))
                ? IrParameter.Self(cls)
                : null;

            var hidden = func.Attributes.Enabled("hidden");

            var start_fn = func.Attributes.Enabled("start_fn");
            var finish_fn = func.Attributes.Enabled("finish_fn");
            var modifier = start_fn ? IrFunctionModifier.Start : (finish_fn ? IrFunctionModifier.Finish : IrFunctionModifier.None);

            return new IrFunction(
                func.Name,
                ParseType(func.Data.ReturnType, func.Attributes),
                [.. func.Data.NamedArgs.Select(ParseParam)], selfParam, hidden, modifier);
        }

        private IrParameter ParseParam(GMIDLNode<GMIDLFunctionArg> param)
        {
            if (param.Data.Optional)
                param.Attributes.Enable("optional");

            return new IrParameter(
                param.Name,
                ParseType(param.Data.Type, param.Attributes),
                param.Data.Optional);
        }

        private IrType ParseType(GMIDLType gmidlType, GMIDLAttributes attributes)
        {
            var primitiveType = gmidlType.PrimitiveOrNull()
                ?? throw new InvalidOperationException("Invalid or unsupported type");

            return ParseType(primitiveType, attributes);
        }

        private IrType ParseType(GMIDLPrimitive? primitiveType, GMIDLAttributes attributes)
        {
            // Base flags from attributes
            var isOptional = attributes.Enabled("optional");
            var isCollection = false;
            int? fixedLen = null;

            // Optional: override / augment using hint syntax, e.g.:
            //   "int32[]"   "Foo[4]"  "Bar?"  "Baz[]?"
            var typeHint = attributes.GetAsString("type_hint") ?? attributes.GetAsString("hint");
            if (!string.IsNullOrWhiteSpace(typeHint))
            {
                // trailing '?'
                var optionalMatch = _matchOptional.Match(typeHint);
                if (optionalMatch.Success)
                {
                    typeHint = typeHint[..^optionalMatch.Value.Length];
                    isOptional = true;
                }

                // trailing [N] or []
                var collectionMatch = _matchCollection.Match(typeHint);
                if (collectionMatch.Success)
                {
                    isCollection = true;
                    typeHint = typeHint[..^collectionMatch.Value.Length];

                    if (collectionMatch.Groups["withLength"].Length > 0)
                        fixedLen = int.Parse(collectionMatch.Groups["withLength"].Value);
                }

                // If hint maps to a primitive, override primitiveType; else treat as Named
                if (_hintToPrimitive.TryGetValue(typeHint, out var hintPrim))
                    primitiveType = hintPrim;
                else
                    primitiveType = null;
            }

            // 1) Parse the *core* (non-nullable, non-collection) type
            var core = primitiveType switch
            {
                GMIDLPrimitive.Double => IrType.Double,
                GMIDLPrimitive.Float => IrType.Float,
                GMIDLPrimitive.Int32 => IrType.Int32,
                GMIDLPrimitive.UInt32 => IrType.UInt32,
                GMIDLPrimitive.Int64 => IrType.Int64,
                GMIDLPrimitive.Bool => IrType.Bool,
                GMIDLPrimitive.String => IrType.String,
                GMIDLPrimitive.CString => IrType.String,

                GMIDLPrimitive.Function => IrType.Function,

                // Dynamic-ish
                GMIDLPrimitive.Array => IrType.AnyArray,
                GMIDLPrimitive.Object => IrType.AnyMap,
                GMIDLPrimitive.GMVal => IrType.Any,

                GMIDLPrimitive.Unit => IrType.Void,

                // Any other primitive not supported -> resolve from hint or throw
                _ => ResolveFromHintOrNamed(typeHint)
            };

            // 2) Apply collection wrapper
            if (isCollection)
                core = IrType.MakeArray(core, fixedLen);

            // 3) Apply nullable wrapper
            if (isOptional)
                core = IrType.MakeNullable(core);

            return core;

            IrType ResolveFromHintOrNamed(string? hint)
            {
                if (string.IsNullOrWhiteSpace(hint))
                    throw new InvalidOperationException("Invalid or unsupported type (no primitive, no hint).");

                // 1) Direct IrType builtins (covers gaps in GMIDLPrimitive)
                if (_hintToIrBuiltin.TryGetValue(hint, out var ir))
                    return ir;

                // 2) Named types (enum/struct)
                return ResolveNamed(hint);
            }

        }

        private IrType ResolveNamed(string name)
        {
            if (_typeSymbols.TryGetValue(name, out var kind))
            {
                return kind switch
                {
                    IrSymbolKind.Enum => new IrType.Named(NamedKind.Enum, name),
                    IrSymbolKind.Struct => new IrType.Named(NamedKind.Struct, name),
                    _ => throw new InvalidOperationException($"Unknown symbol kind for '{name}'.")
                };
            }

            // You can choose to throw here instead (stricter),
            // but keeping "unknown named type" visible is often useful during iteration.
            throw new InvalidOperationException($"Unknown named type '{name}'. Did you forget to declare it?");
        }

        private static ImmutableArray<IrStruct> TopologicallySortStructs(ImmutableArray<IrStruct> structs)
        {
            // Topological sort structs by dependency order using Kahn's algorithm.
            // Why? Code generators need structs defined before they're used as field types.
            // Example: if struct B has a field of type A, then A must be emitted before B.
            //
            // Algorithm:
            // 1. Build dependency graph: edge from A->B means "B depends on A"
            // 2. Calculate in-degree for each node (how many dependencies it has)
            // 3. Start with all 0-indegree nodes (no dependencies)
            // 4. Process queue: emit node, decrement neighbors' in-degree, add newly-0 nodes
            // 5. If we don't process all nodes, there's a cycle (error)

            var byName = structs.ToDictionary(s => s.Name, StringComparer.Ordinal);

            // Adjacency list: dep -> [dependents]
            var adj = structs.ToDictionary(
                s => s.Name,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);

            // In-degree: how many dependencies each struct has
            var indeg = structs.ToDictionary(s => s.Name, _ => 0, StringComparer.Ordinal);

            // Build graph: for each struct, find its dependencies, add edges dep->struct
            foreach (var s in structs)
            {
                var deps = CollectStructDeps(s);
                foreach (var d in deps)
                {
                    if (!adj.ContainsKey(d)) continue;  // Dependency not in this compilation (external type)
                    if (adj[d].Add(s.Name))
                        indeg[s.Name]++;
                }
            }

            // Preserve original order as a tiebreaker (stable sort)
            var pos = structs.Select((s, i) => (s.Name, i))
                             .ToDictionary(t => t.Name, t => t.i, StringComparer.Ordinal);

            // Start with structs that have no dependencies
            var q = new Queue<string>(
                indeg.Where(kv => kv.Value == 0)
                     .OrderBy(kv => pos[kv.Key])
                     .Select(kv => kv.Key));

            var ordered = new List<IrStruct>(structs.Length);

            // Process queue: emit struct, decrement dependents' in-degree
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                ordered.Add(byName[u]);

                foreach (var v in adj[u])
                {
                    if (--indeg[v] == 0)
                        q.Enqueue(v);
                }
            }

            // If we didn't process all structs, there must be a cycle
            if (ordered.Count != structs.Length)
                throw new InvalidOperationException("Struct dependency cycle detected.");

            return [.. ordered];

            // ---- local helpers ----

            static HashSet<string> CollectStructDeps(IrStruct s)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var f in s.Fields)
                    CollectFromType(f.Type, set);
                return set;
            }

            static void CollectFromType(IrType t, HashSet<string> set)
            {
                // array<T> depends on T
                if (t is IrType.Array a)
                {
                    CollectFromType(a.Element, set);
                    return;
                }

                // optional<T> depends on T
                if (t is IrType.Nullable n)
                {
                    CollectFromType(n.Underlying, set);
                    return;
                }

                // struct dependency
                if (t is IrType.Named { Kind: NamedKind.Struct, Name: var name })
                    set.Add(name);
            }
        }

        [GeneratedRegex(@"\[(?<withLength>\d*)\]$", RegexOptions.Compiled)]
        private static partial Regex MatchCollection();

        [GeneratedRegex(@"(?<opt>\?)$", RegexOptions.Compiled)]
        private static partial Regex MatchOptional();
    }
}
