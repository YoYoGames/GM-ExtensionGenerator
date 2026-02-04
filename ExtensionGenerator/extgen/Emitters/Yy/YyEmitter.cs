using codegencore.Model;
using extgen.Emitters.Doc;
using extgen.Model;
using extgen.Options;
using System.IO;
using System.Text;
using System.Text.Json;


namespace extgen.Emitters.Yy
{

    /// <summary>
    /// Writes the function-declaration chunk developers need to *paste*
    /// into their *.yy* extension file until direct injection is available.
    /// </summary>
    internal sealed class YyEmitter(YyEmitterOptions options, RuntimeNaming runtime) : IIrEmitter
    {
        // currently unused, but keep for future expansion (links/namespace hints etc)
        private readonly YyEmitterOptions _options = options;
        private readonly RuntimeNaming _runtime = runtime;

        public void Emit(IrCompilation comp, string outputDir)
        {
            YyEmitterContext ctx = new(comp.Name, _options, runtime);
            var layout = new YyLayout(outputDir, _options);

            var path = Path.Combine(layout.OutputDir, $"{string.Format(layout.OutputFile, ctx.ExtName)}.yy");

            EmitAll(comp, ctx, path);
        }

        private static void EmitAll(IrCompilation comp, YyEmitterContext ctx, string path)
        {
            // 8-bit clean; no BOM; one object per line
            using var sw = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            using var ms = new MemoryStream();
            var jsonWriterOptions = new JsonWriterOptions { Indented = false };
            
            EmitFunctions(comp, ctx, sw, ms, jsonWriterOptions);
        }

        private static void EmitFunctions(IrCompilation comp, YyEmitterContext ctx, StreamWriter sw, MemoryStream ms, JsonWriterOptions jsonWriterOptions)
        {
            var usesFunctions = comp.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
            var usesBuffer = comp.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer)));

            foreach (var fn in comp.Functions)
            {
                ms.SetLength(0);                         // reset buffer
                using (var jw = new Utf8JsonWriter(ms, jsonWriterOptions))
                {
                    WriteFn(ctx, jw, fn);                     // ← your existing method
                }

                // Convert the UTF-8 bytes into a string and write one line
                sw.WriteLine($"{Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length)},");
            }

            // Write utility function to handle function calls from native code
            if (usesFunctions)
            {
                ms.SetLength(0);
                using (var jw = new Utf8JsonWriter(ms, jsonWriterOptions))
                {
                    WriteInvocationHandlerFn(ctx, jw, comp.Name);
                }
                sw.WriteLine($"{Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length)},");
            }

            if (usesBuffer)
            {
                ms.SetLength(0);
                using (var jw = new Utf8JsonWriter(ms, jsonWriterOptions))
                {
                    WriteQueueBufferFn(ctx, jw, comp.Name);
                }
                sw.WriteLine($"{Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length)},");
            }
        }

        private static void WriteFn(YyEmitterContext ctx, Utf8JsonWriter jw, IrFunction fn)
        {
            bool needArgsBuf = IrAnalysis.NeedsArgsBuffer(fn);
            bool needRetBuf = IrAnalysis.NeedsRetBuffer(fn);

            // --- build GM arg list ------------------------------------------------
            var args = new List<int>();
            var docs = new List<string>();

            if (needArgsBuf)
            {                       // char* + length
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
                        args.Add(1); // char*
                        docs.Add($"@param {{String}} {p.Name}");
                    }
                    else
                    {
                        args.Add(2); // double
                        docs.Add($"@param {{Real}} {p.Name}");
                    }
                }
            }

            int returnType = 2;                    // 2 = double
            if (needRetBuf)
            {
                args.AddRange([1, 2]);             // buf + len
                docs.Add("@param {Pointer} _ret_buffer");
                docs.Add("@param {Real} _ret_buffer_length");
            }
            else if (fn.ReturnType is IrType.Builtin { Kind: BuiltinKind.String })
            {
                returnType = 1;                    // 1 = char*
            }


            docs.Add($"@returns {{{(returnType == 1 ? "String" : "Real")}}}");

            // --- emit JSON object in GameMaker’s exact order ---------------------
            jw.WriteStartObject();

            jw.WriteString("$GMExtensionFunction", "");
            jw.WriteString("%Name", needArgsBuf || needRetBuf ? $"__{fn.Name}" : fn.Name);
            jw.WriteNumber("argCount", args.Count);

            jw.WritePropertyName("args");
            jw.WriteStartArray();
            foreach (var a in args) jw.WriteNumberValue(a);
            jw.WriteEndArray();

            jw.WriteString("documentation", $"{string.Join(Environment.NewLine, docs)}");
            jw.WriteString("externalName", $"{ctx.Runtime.NativePrefix}{fn.Name}");
            jw.WriteString("help", "");
            jw.WriteBoolean("hidden", needArgsBuf || needRetBuf);
            jw.WriteNumber("kind", 4);
            jw.WriteString("name", needArgsBuf || needRetBuf ? $"__{fn.Name}" : fn.Name);
            jw.WriteString("resourceType", "GMExtensionFunction");
            jw.WriteString("resourceVersion", "2.0");
            jw.WriteNumber("returnType", returnType);

            jw.WriteEndObject();
        }

        private static void WriteInvocationHandlerFn(YyEmitterContext ctx, Utf8JsonWriter jw, string extensionName)
        {
            jw.WriteStartObject();

            jw.WriteString("$GMExtensionFunction", "");
            jw.WriteString("%Name", $"__{extensionName}_invocation_handler");
            jw.WriteNumber("argCount", 2);

            jw.WritePropertyName("args");
            jw.WriteStartArray();
            jw.WriteNumberValue(1); // Pointer
            jw.WriteNumberValue(2); // Length
            jw.WriteEndArray();

            jw.WriteString("documentation", $"{string.Join(Environment.NewLine, ["@param {Pointer} _buffer_ptr", "@param {Real} _buffer_size"])}");
            jw.WriteString("externalName", $"{ctx.Runtime.NativePrefix}{extensionName}_invocation_handler");
            jw.WriteString("help", "");
            jw.WriteBoolean("hidden", true);
            jw.WriteNumber("kind", 4);
            jw.WriteString("name", $"__{extensionName}_invocation_handler");
            jw.WriteString("resourceType", "GMExtensionFunction");
            jw.WriteString("resourceVersion", "2.0");
            jw.WriteNumber("returnType", 2);

            jw.WriteEndObject();
        }

        private static void WriteQueueBufferFn(YyEmitterContext ctx, Utf8JsonWriter jw, string extensionName)
        {
            jw.WriteStartObject();

            jw.WriteString("$GMExtensionFunction", "");
            jw.WriteString("%Name", $"__{extensionName}_queue_buffer");
            jw.WriteNumber("argCount", 2);

            jw.WritePropertyName("args");
            jw.WriteStartArray();
            jw.WriteNumberValue(1); // Pointer
            jw.WriteNumberValue(2); // Length
            jw.WriteEndArray();

            jw.WriteString("documentation", $"{string.Join(Environment.NewLine, ["@param {Pointer} _buffer_ptr", "@param {Real} _buffer_size"])}");
            jw.WriteString("externalName", $"{ctx.Runtime.NativePrefix}{extensionName}_queue_buffer");
            jw.WriteString("help", "");
            jw.WriteBoolean("hidden", true);
            jw.WriteNumber("kind", 4);
            jw.WriteString("name", $"__{extensionName}_queue_buffer");
            jw.WriteString("resourceType", "GMExtensionFunction");
            jw.WriteString("resourceVersion", "2.0");
            jw.WriteNumber("returnType", 2);

            jw.WriteEndObject();
        }
    }

}