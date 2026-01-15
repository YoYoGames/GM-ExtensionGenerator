using extgen.Model;
using gmidlreader;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace extgen.Parsing.Gmidl
{
    internal enum IrSymbolKind { Enum, Struct /* later: Alias, Interface, etc */ }

    internal sealed record IrSymbol(IrSymbolKind Kind, string Name);

    internal partial class GmidlSchemaParser(GMIDLDatabase db)
    {
        private readonly GMIDLDatabase _db = db;

        private static readonly Regex _matchCollection = MatchCollection();
        private static readonly Regex _matchOptional = MatchOptional();

        private static ImmutableArray<IrEnum> _enums = [];
        private static ImmutableArray<IrStruct> _structs = [];
        private static ImmutableArray<IrConstant> _constants = [];
        private static ImmutableArray<IrFunction> _functions = [];

        private Dictionary<string, IrSymbolKind> _typeSymbols = new(StringComparer.Ordinal);

        public IrCompilation Build()
        {
            if (_db.Modules.Count == 0) return new IrCompilation("", [], [], [], []);

            var module = _db.Modules[0];

            _typeSymbols = new(StringComparer.Ordinal);

            foreach (var e in module.Data.Enums)
                AddSymbol(e.Name, IrSymbolKind.Enum);

            foreach (var c in module.Data.Classes)
                AddSymbol(c.Name[2..^2], IrSymbolKind.Struct);

            void AddSymbol(string name, IrSymbolKind kind)
            {
                if (_typeSymbols.TryGetValue(name, out var existing) && existing != kind)
                    throw new InvalidOperationException($"Type name collision: '{name}' is both {existing} and {kind}.");
                _typeSymbols[name] = kind;
            }

            _enums = [.. module.Data.Enums.Select(ParseEnum)];
            _structs = [.. module.Data.Classes.Select(ParseStruct)];
            _constants = [.. module.Data.Constants.Select(ParseConstant)];
            _functions = [.. module.Data.Functions.Select(ParseFunction)];

            return new IrCompilation(module.Name, _enums, TopologicallySortStructs(_structs), _functions, _constants);
        }

        private IrConstant ParseConstant(GMIDLNode<GMIDLConstant> cst) 
        {
            var primitiveType = cst.Data.Type.PrimitiveOrNull() ?? throw new InvalidOperationException("Constants need to have a defined type");
            return new IrConstant(cst.Name, ParseType(primitiveType, cst.Attributes), cst.Data.Value ?? "");
        }

        private IrEnum ParseEnum(GMIDLNode<GMIDLEnum> enm) 
        {
            var primitiveType = enm.Data.DefaultType.PrimitiveOrNull() ?? GMIDLPrimitive.Int32;
            return new IrEnum(enm.Name, ParseType(primitiveType, enm.Attributes), [.. enm.Data.Elements.Select(ParseEnumMember)]);
        }

        private IrEnumMember ParseEnumMember(GMIDLNode<GMIDLEnumElement> enm)
        {
            return new IrEnumMember(enm.Name, enm.Data.Value);
        }

        private IrStruct ParseStruct(GMIDLNode<GMIDLClass> cls)
        {
            return new(cls.Name[2..^2], [.. cls.Data.Properties.Where(p => p.Attributes.Enabled("field")).Select(ParseField)]);
        }

        private IrField ParseField(GMIDLNode<GMIDLProperty> f)
        {
            var attributes = f.Attributes;
            var value = attributes.GetAsString("value") ?? attributes.GetAsString("value");

            bool v = bool.TryParse(f.Attributes.GetAsString("optional") ?? "false", out bool isOptional);
            var type = ParseType(f.Data.Type, f.Attributes);

            return new(f.Name, type, null, !isOptional, Value: value);
        }

        private IrFunction ParseFunction(GMIDLNode<GMIDLFunction> func)
        {
            return new(func.Name, ParseType(func.Data.ReturnType, func.Attributes), [.. func.Data.NamedArgs.Select(ParseParam)]);
        }

        private IrParameter ParseParam(GMIDLNode<GMIDLFunctionArg> param)
        {
            if (param.Data.Optional) param.Attributes.Enable("optional");
            return new(param.Name, ParseType(param.Data.Type, param.Attributes), param.Data.Optional);
        }

        private IrType ParseType(GMIDLType gmidlType, GMIDLAttributes attributes)
        {
            var primitiveType = gmidlType.PrimitiveOrNull() ?? throw new InvalidOperationException("Invalid or not supported type");
            return ParseType(primitiveType, attributes);
        }

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
            ["object"] = GMIDLPrimitive.Object,
            ["array"] = GMIDLPrimitive.Array,
            ["any"] = GMIDLPrimitive.GMVal,
            ["func"] = GMIDLPrimitive.Function,
            ["function"] = GMIDLPrimitive.Function,
        };

        private IrType ResolveNamed(string name, bool isOptional, bool isCollection, int? withLength)
        {
            if (_typeSymbols.TryGetValue(name, out var kind))
            {
                return kind switch
                {
                    IrSymbolKind.Enum =>
                        new IrType(IrTypeKind.Enum, name,
                            isCollection, withLength, isOptional, Underlying: _enums.First(e => e.Name == name).Underlying),

                    IrSymbolKind.Struct =>
                        new IrType(IrTypeKind.Struct, name,
                            isCollection, withLength, isOptional),

                    _ => throw new InvalidOperationException($"Unknown symbol kind for '{name}'.")
                };
            }

            return new IrType(IrTypeKind.Error, name, isCollection, withLength, isOptional);
        }

        private IrType ParseType(GMIDLPrimitive? primitiveType, GMIDLAttributes attributes)
        {
            var isOptional = attributes.Enabled("optional");
            var isCollection = false;
            int? withLength = null;

            var typeHint = attributes.GetAsString("type_hint") ?? attributes.GetAsString("hint");
            if (typeHint is not null)
            {
                var optionalMatch = _matchOptional.Match(typeHint);
                if (optionalMatch.Success)
                {
                    typeHint = typeHint[..^optionalMatch.Value.Length];
                    isOptional = true;
                }

                var collectionMatch = _matchCollection.Match(typeHint);
                if (collectionMatch.Success)
                {
                    isCollection = true;
                    typeHint = typeHint[..^collectionMatch.Value.Length];
                    if (collectionMatch.Groups["withLength"].Length > 0)
                        withLength = int.Parse(collectionMatch.Groups["withLength"].Value);
                }

                primitiveType = _hintToPrimitive.TryGetValue(typeHint, out var value) ? value : null;
            }

            return primitiveType switch
            {
                GMIDLPrimitive.Double => IrType.Double with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.Float => IrType.Float with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.Int32 => IrType.Int32 with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.UInt32 => IrType.UInt32 with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.Int64 => IrType.Int64 with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.Bool => IrType.Bool with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.String => IrType.String with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.CString => IrType.String with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.Function => IrType.Function with { IsNullable = isOptional, IsCollection = isCollection, FixedLength = withLength },
                GMIDLPrimitive.Array => IrType.AnyArray with { IsNullable = isOptional },
                GMIDLPrimitive.Object => IrType.AnyMap with { IsNullable = isOptional },
                GMIDLPrimitive.Unit => IrType.Void,
                GMIDLPrimitive.GMVal => IrType.Any,
                GMIDLPrimitive.Boxed => throw new NotImplementedException(),
                GMIDLPrimitive.Pointer => throw new NotImplementedException(),
                _ => typeHint is null
                        ? throw new InvalidOperationException("Invalid or unsupported type")
                        : ResolveFromHint(typeHint, isOptional, isCollection, withLength)
            };

            IrType ResolveFromHint(string hint, bool isOpt, bool isColl, int? len)
            {
                // builtin keywords that are not in GMIDLPrimitive mapping, if any:
                if (hint.Equals("buffer", StringComparison.OrdinalIgnoreCase))
                    return IrType.Buffer with { IsNullable = isOpt, IsCollection = isColl, FixedLength = len };

                if (hint.Equals("func", StringComparison.OrdinalIgnoreCase) ||
                    hint.Equals("function", StringComparison.OrdinalIgnoreCase))
                    return IrType.Function with { IsNullable = isOpt, IsCollection = isColl, FixedLength = len };

                // named types (enum/struct)
                return ResolveNamed(hint, isOpt, isColl, len);
            }
        }

        private static ImmutableArray<IrStruct> TopologicallySortStructs(ImmutableArray<IrStruct> structs)
        {
            var byName = structs.ToDictionary(s => s.Name, StringComparer.Ordinal);

            // build empty adjacency and indegree
            var adj = structs.ToDictionary(s => s.Name, _ => new HashSet<string>(StringComparer.Ordinal),
                                            StringComparer.Ordinal);
            var indeg = structs.ToDictionary(s => s.Name, _ => 0, StringComparer.Ordinal);

            // for each struct s, for each dependency d that s uses-by-value,
            // add edge d -> s and bump indegree(s)
            foreach (var s in structs)
            {
                var depsOfS = CollectStructDeps(s);
                foreach (var d in depsOfS)
                {
                    if (!adj.ContainsKey(d)) continue;        // ignore unknowns
                    if (adj[d].Add(s.Name))
                        indeg[s.Name]++;
                }
            }

            // Kahn's: start with nodes whose indegree==0 (true leaves = no deps)
            var pos = structs.Select((s, i) => (s.Name, i))
                             .ToDictionary(t => t.Name, t => t.i, StringComparer.Ordinal);

            var q = new Queue<string>(indeg.Where(kv => kv.Value == 0)
                                           .OrderBy(kv => pos[kv.Key])
                                           .Select(kv => kv.Key));

            var ordered = new List<IrStruct>(structs.Length);
            while (q.Count > 0)
            {
                var u = q.Dequeue();
                ordered.Add(byName[u]);
                foreach (var v in adj[u])
                    if (--indeg[v] == 0) q.Enqueue(v);
            }

            if (ordered.Count != structs.Length)
                throw new InvalidOperationException("Struct dependency cycle detected …");

            return [.. ordered];

            // --- local helpers ---

            static HashSet<string> CollectStructDeps(IrStruct s)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var f in s.Fields)
                    CollectFromType(f.Type, set);
                return set;
            }

            static void CollectFromType(IrType t, HashSet<string> set)
            {
                // Collections still require complete element type (vector<T>, array<T,N>)
                if (t.IsCollection)
                {
                    var el = t with { IsCollection = false, FixedLength = null };
                    CollectFromType(el, set);
                    return;
                }

                // Nullable (optional<T>) also needs complete T at class definition
                if (t.IsNullable)
                {
                    var inner = t with { IsNullable = false };
                    CollectFromType(inner, set);
                    return;
                }

                if (t.Kind == IrTypeKind.Struct)
                    set.Add(t.Name);

                // If you later support e.g. Map<string, Struct>, Variant<Struct,...>, add cases here.
            }
        }

        [GeneratedRegex(@"\[(?<withLength>\d*)\]$", RegexOptions.Compiled)]
        private static partial Regex MatchCollection();

        [GeneratedRegex(@"(?<opt>\?)$", RegexOptions.Compiled)]
        private static partial Regex MatchOptional();
    }
}
