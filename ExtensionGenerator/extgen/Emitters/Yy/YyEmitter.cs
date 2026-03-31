using codegencore.Models;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace extgen.Emitters.Yy
{
    /// <summary>
    /// Emits GameMaker .yy extension definition files.
    /// Supports both patching existing .yy files and generating plain function definitions.
    /// </summary>
    internal sealed class YyEmitter(YyEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        public void Emit(IrCompilation comp, string outputDir)
        {
            YyEmitterContext ctx = new(comp.Name, settings, runtime);
            var layout = new YyLayout(outputDir, settings);

            var path = Path.Combine(layout.OutputDir, $"{string.Format(layout.OutputFile, ctx.ExtName)}");

            switch (settings.Mode)
            {
                case YyEmitterMode.Patch:
                    PatchYyFile(comp, ctx, path);
                    break;
                case YyEmitterMode.Plain:
                    WritePlainYyDefinitions(comp, ctx, path);
                    break;
            }
        }

        private static void PatchYyFile(IrCompilation comp, YyEmitterContext ctx, string yyPath)
        {
            if (!File.Exists(yyPath))
                throw new FileNotFoundException("YY extension file not found. Ensure you have an extension created.", yyPath);

            var text = File.ReadAllText(yyPath, Encoding.UTF8);

            // .yy files commonly contain trailing commas -> allow them
            var docOptions = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            JsonNode? rootNode = JsonNode.Parse(text, documentOptions: docOptions);
            if (rootNode is not JsonObject root)
                throw new InvalidDataException("YY root was not an object. Invalid extension path provided.");

            var extname = root["name"]?.GetValue<string>();
            var expectedExtensionName = ctx.Settings.ExtensionName ?? comp.Name;
            if (extname is null || string.Compare(extname, expectedExtensionName) != 0) 
            {
                throw new InvalidDataException($"YY extension name doesn't match settings - '{expectedExtensionName}'");
            }

            if (ctx.Settings.AndroidEnabled)
                PatchYyAndroidOptions(comp, ctx, root);
            if (ctx.Settings.IosEnabled)
                PatchYyIosOptions(comp, ctx, root);
            if (ctx.Settings.TvosEnabled)
                PatchYyTvosOptions(comp, ctx, root);

            // Navigate: root["files"] - array of GMExtensionFile entries
            if (root["files"] is not JsonArray filesArray)
                throw new InvalidDataException("YY did not contain a 'files' array.");

            // Find the file entry matching "{compilation.Name}.ext"
            var expectedFilename = ctx.Settings.ExtensionFileName ?? $"{comp.Name}.ext";
            JsonObject? extFileObj = FindExtensionFileObject(filesArray, expectedFilename) ?? 
                throw new InvalidDataException($"YY extension should have a pre-created generic file - '{expectedFilename}'");

            var allFunctions = comp.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
            extFileObj["final"] = allFunctions.Where(f => f.Modifier == IrFunctionModifier.Finish).FirstOrDefault()?.Name ?? "";
            extFileObj["init"] = allFunctions.Where(f => f.Modifier == IrFunctionModifier.Start).FirstOrDefault()?.Name ?? "";

            PatchYyFunctions(comp, ctx, extFileObj);

            // Write back (GM will normalize formatting; you said that’s fine)
            WriteBack(yyPath, root);
        }

        private static void PatchYyAndroidOptions(IrCompilation comp, YyEmitterContext ctx, JsonObject root)
        {
            root["androidclassname"] = comp.Name;
        }

        private static void PatchYyIosOptions(IrCompilation comp, YyEmitterContext ctx, JsonObject root)
        {
            root["classname"] = comp.Name;
            EnsureAppleLinkerFlags(root, "maclinkerflags", "-ObjC");

            if (ctx.Settings.PatchFrameworks)
                EnsureThirdPartyXcframeworkEntry(root, "iosThirdPartyFrameworkEntries", ctx.ExtName);
        }

        private static void PatchYyTvosOptions(IrCompilation comp, YyEmitterContext ctx, JsonObject root)
        {
            root["tvosclassname"] = comp.Name;
            EnsureAppleLinkerFlags(root, "tvosmaclinkerflags", "-ObjC");

            if (ctx.Settings.PatchFrameworks)
                EnsureThirdPartyXcframeworkEntry(root, "tvosThirdPartyFrameworkEntries", ctx.ExtName);
        }

        private static void WritePlainYyDefinitions(IrCompilation comp, YyEmitterContext ctx, string yyPath) 
        {
            if (Path.GetExtension(yyPath).Equals(".yy", StringComparison.InvariantCultureIgnoreCase))
                throw new FileNotFoundException("YY emitter :: Plain writting mode doesn't support .yy file extension.", yyPath);

            var writeOptions = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            var output = string.Join($",{Environment.NewLine}", BuildFunctionJsonObjectList(comp, ctx).Select(fo => fo.ToJsonString(writeOptions)));

            File.WriteAllText(yyPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static void PatchYyFunctions(IrCompilation comp, YyEmitterContext ctx, JsonObject extFileObj)
        {
            // Build the new functions list (IrFunction -> YyExtFunction -> JsonObject)
            var newFunctions = new JsonArray();
            foreach (var func in BuildFunctionJsonObjectList(comp, ctx))
            {
                newFunctions.Add(func);
            }
            // Replace only the "functions" array
            extFileObj["functions"] = newFunctions;
        }

        private static IEnumerable<JsonObject> BuildFunctionJsonObjectList(IrCompilation comp, YyEmitterContext ctx)
        {
            var allFunctions = comp.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
            var usesFunctions = comp.HasFunctionType();
            var usesBuffer = comp.HasBufferType();

            foreach (var fn in allFunctions)
            {
                var yyFn = YyExtFunction.FromIr(ctx, fn);
                yield return yyFn.ToJsonObject();
            }

            if (usesFunctions)
                yield return YyExtFunction.MakeInvocationHandler(ctx, comp.Name).ToJsonObject();

            if (usesBuffer)
                yield return YyExtFunction.MakeQueueBuffer(ctx, comp.Name).ToJsonObject();

            yield break;
        }

        // Helper: patch GameMaker .yy file to add required Apple linker flags
        private static void EnsureAppleLinkerFlags(JsonObject root, string key, params string[] flags)
        {
            // GameMaker .yy extension files store platform-specific linker flags as space-separated strings.
            // For iOS/tvOS Objective-C++, we need "-ObjC" flag to force linker to load all Objective-C
            // categories from static libraries. Without this, methods added via categories won't be linked,
            // causing runtime crashes when GML tries to call them.
            //
            // This function idempotently adds flags to the existing string (no duplicates, preserves order).

            if (root is null) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
            if (flags is null || flags.Length == 0) return;

            // Normalize input flags: trim, drop empties, de-dupe (preserve order)
            var desired = new List<string>();
            var seenDesired = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in flags)
            {
                var t = (f ?? "").Trim();
                if (t.Length == 0) continue;
                if (seenDesired.Add(t)) desired.Add(t);
            }
            if (desired.Count == 0) return;

            // Parse existing flags (may contain whitespace separators like spaces, tabs, newlines)
            string? current = root[key]?.GetValue<string>();
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(current))
            {
                parts = current
                    .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }

            var existing = new HashSet<string>(parts, StringComparer.Ordinal);

            // Add missing flags
            bool changed = false;
            foreach (var f in desired)
            {
                if (existing.Add(f))
                {
                    parts.Add(f);
                    changed = true;
                }
            }

            // Write back normalized (space-separated, no duplicates)
            if (string.IsNullOrWhiteSpace(current) || changed)
                root[key] = string.Join(" ", parts);
        }

        private static void EnsureThirdPartyXcframeworkEntry(JsonObject root, string key, string extName)
        {
            var wanted = $"{extName}.xcframework";

            // Get or create array
            JsonArray arr;
            if (root[key] is JsonArray existing)
            {
                arr = existing;
            }
            else
            {
                arr = new JsonArray();
                root[key] = arr;
            }

            // Check if already present (by "name" or "%Name")
            bool exists = arr.OfType<JsonObject>().Any(o =>
            {
                var name = o["name"]?.GetValue<string>();
                var pctName = o["%Name"]?.GetValue<string>();
                return string.Equals(name, wanted, StringComparison.Ordinal)
                    || string.Equals(pctName, wanted, StringComparison.Ordinal);
            });

            if (exists)
                return;

            // Append new framework entry
            var entry = new JsonObject
            {
                ["$GMExtensionFrameworkEntry"] = "",
                ["%Name"] = wanted,
                ["embed"] = 0,
                ["name"] = wanted,
                ["resourceType"] = "GMExtensionFrameworkEntry",
                ["resourceVersion"] = "2.0",
                ["weakReference"] = false
            };

            arr.Add(entry);
        }

        private static void WriteBack(string yyPath, JsonObject root)
        {
            var writeOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var output = root.ToJsonString(writeOptions);
            File.WriteAllText(yyPath, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static JsonObject? FindExtensionFileObject(JsonArray filesArray, string expectedFilename)
        {
            foreach (var node in filesArray)
            {
                if (node is not JsonObject obj) continue;
                var filename = obj["filename"]?.GetValue<string>();
                if (string.Equals(filename, expectedFilename, StringComparison.Ordinal))
                    return obj;
            }
            return null;
        }
    }

    /// <summary>
    /// Minimal model for a GM extension function entry inside a .yy "functions" array.
    /// </summary>
    internal sealed class YyExtFunction
    {
        public string InternalName { get; init; } = "";
        public string ExternalName { get; init; } = "";
        public int ArgCount { get; init; }
        public List<int> Args { get; init; } = [];
        public string Documentation { get; init; } = "";
        public bool Hidden { get; init; }
        public int ReturnType { get; init; } // 1=string(char*), 2=real(double)

        // Constants typical for GMExtensionFunction entries
        private const int Kind = 4;
        private const string ResourceType = "GMExtensionFunction";
        private const string ResourceVersion = "2.0";

        public JsonObject ToJsonObject()
        {
            // JsonObject preserves insertion order of added properties,
            // so we can keep the “GameMaker-ish” order you care about.
            var o = new JsonObject();

            o.Add("$GMExtensionFunction", "");
            o.Add("%Name", InternalName);
            o.Add("argCount", ArgCount);

            var args = new JsonArray();
            foreach (var a in Args) args.Add(a);
            o.Add("args", args);

            o.Add("documentation", Documentation);
            o.Add("externalName", ExternalName);
            o.Add("help", "");
            o.Add("hidden", Hidden);
            o.Add("kind", Kind);
            o.Add("name", InternalName);
            o.Add("resourceType", ResourceType);
            o.Add("resourceVersion", ResourceVersion);
            o.Add("returnType", ReturnType);

            return o;
        }

        public static YyExtFunction FromIr(YyEmitterContext ctx, IrFunction fn)
        {
            bool needArgsBuf = IrAnalysis.NeedsArgsBuffer(fn);
            bool needRetBuf = IrAnalysis.NeedsRetBuffer(fn);

            var args = new List<int>();
            var docs = new List<string>();

            if (needArgsBuf)
            {
                // char* + length
                args.AddRange([1, 2]);
                docs.Add("@param {Pointer} _arg_buffer");
                docs.Add("@param {Real} _arg_buffer_length");
            }
            else
            {
                foreach (var p in IrAnalysis.DirectArgs(fn))
                {
                    if (p.Type is IrType.Builtin { Kind: BuiltinKind.String })
                    {
                        args.Add(1);
                        docs.Add($"@param {{String}} {p.Name}");
                    }
                    else
                    {
                        args.Add(2);
                        docs.Add($"@param {{Real}} {p.Name}");
                    }
                }
            }

            int returnType = 2;
            if (needRetBuf)
            {
                args.AddRange(new[] { 1, 2 });
                docs.Add("@param {Pointer} _ret_buffer");
                docs.Add("@param {Real} _ret_buffer_length");
            }
            else if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.String })
            {
                returnType = 1;
            }

            docs.Add($"@returns {{{(returnType == 1 ? "String" : "Real")}}}");

            var internalName = (needArgsBuf || needRetBuf) ? $"__{fn.Name}" : fn.Name;
            var externalName = $"{ctx.Runtime.NativePrefix}{fn.Name}";

            return new YyExtFunction
            {
                InternalName = internalName,
                ExternalName = externalName,
                ArgCount = args.Count,
                Args = args,
                Documentation = string.Join(Environment.NewLine, docs),
                Hidden = (needArgsBuf || needRetBuf) || fn.Hidden,
                ReturnType = returnType
            };
        }

        public static YyExtFunction MakeInvocationHandler(YyEmitterContext ctx, string extensionName)
        {
            return new YyExtFunction
            {
                InternalName = $"__{extensionName}_invocation_handler",
                ExternalName = $"{ctx.Runtime.NativePrefix}{extensionName}_invocation_handler",
                ArgCount = 2,
                Args = [1, 2],
                Documentation = string.Join(Environment.NewLine, new[]
                {
                    "@param {Pointer} _buffer_ptr",
                    "@param {Real} _buffer_size"
                }),
                Hidden = true,
                ReturnType = 2
            };
        }

        public static YyExtFunction MakeQueueBuffer(YyEmitterContext ctx, string extensionName)
        {
            return new YyExtFunction
            {
                InternalName = $"__{extensionName}_queue_buffer",
                ExternalName = $"{ctx.Runtime.NativePrefix}{extensionName}_queue_buffer",
                ArgCount = 2,
                Args = new List<int> { 1, 2 },
                Documentation = string.Join(Environment.NewLine, new[]
                {
                    "@param {Pointer} _buffer_ptr",
                    "@param {Real} _buffer_size"
                }),
                Hidden = true,
                ReturnType = 2
            };
        }
    }
}
