using codegencore.Writers;
using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using extgen.Emitters.Doc;
using extgen.Model;
using extgen.Options;
using extgencore.Helpers;

namespace extgen.Emitters
{
    internal sealed record GmlEmitterContext(string ExtName, GmlEmitterOptions Opts) { }

    public sealed class GmlEmitter(GmlEmitterOptions opts) : IIrEmitter
    {
        private readonly GmlEmitterOptions _opts = opts ?? throw new ArgumentNullException(nameof(opts));

        private static readonly string internalArgBuffer = "__args_buffer";
        private static readonly string internalRetBuffer = "__ret_buffer";

        private static readonly string extCoreArgsBuffer = "__ext_core_get_args_buffer";
        private static readonly string extCoreRetBuffer = "__ext_core_get_ret_buffer";
        private static readonly string extCoreMarshalValue = "__ext_core_buffer_marshal_value";
        private static readonly string extCoreUnmarshalValue = "__ext_core_buffer_unmarshal_value";
        private static readonly string extCoreFunctionRegister = "__ext_core_function_register";
        private static readonly string extCoreFunctionDispatcher = "__GMNativeFunctionDispatcher";

        public void Emit(IrCompilation comp, string dir)
        {
            // Default baseDir to current directory if not provided
            dir = string.IsNullOrWhiteSpace(dir) ? Environment.CurrentDirectory : dir;

            // Expand %VARS% / $VARS (we want to write the code to a single file)
            dir = Environment.ExpandEnvironmentVariables(dir);
            string outputFile = Environment.ExpandEnvironmentVariables(_opts.OutputFile);
            outputFile = Path.GetFullPath(outputFile, dir);

            var ctx = new GmlEmitterContext(comp.Name, _opts);

            // This files needs to already exist in the system we shouldn't be creating a new file.
            if (!File.Exists(outputFile)) 
            {
                throw new ArgumentException($"GML Emitter: the output file path doesn't exist ({outputFile}).");
            }

            using TextWriter textWriter = new StreamWriter(outputFile);
            var iw = CodeWriter.From(textWriter, "    ");
            var writer = new GmlWriter(iw);
            EmitGml(ctx, comp, writer);
        }

        // ----------
        private static void EmitGml(GmlEmitterContext ctx, IrCompilation c, GmlWriter w)
        {
            w.Line("/// Auto-generated – do not edit\n");

            // 0. enums
            w.Section("Macros").Line();
            foreach (var cst in c.Constants)
            {
                EmitConstant(cst, w);
                w.Line();
            }

            w.Section("Enums").Line();
            foreach (var e in c.Enums)
            {
                EmitEnum(e, w);
                w.Line();
            }

            // 1. struct constructors
            w.Section("Constructors").Line();
            foreach (var s in c.Structs)
            {
                EmitStruct(ctx, s, w);
                w.Line();
            }

            // 2. struct codecs
            w.Section("Codecs").Line();
            foreach (var s in c.Structs)
            {
                EmitEncoder(ctx, s, w);
                w.Line();
                EmitDecoder(ctx, s, w);
                w.Line();
            }

            // 3. function wrappers (same encode/decode helpers)
            w.Section("Functions").Line();
            foreach (var fn in c.Functions)
            {
                EmitWrapper(ctx, c, fn, w);
                w.Line();
            }

            w.Line("/// @ignore");
            w.Function($"__{c.Name}_get_decoders", [], funcBody =>
            {
                funcBody.Assign("__decoders", expr => expr.ArrayLiteral(c.Structs.Select(s => $"__{s.Name}_decode"), true), VariableScope.Static);
                funcBody.Return("__decoders");
            });

            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.Kind == IrTypeKind.Function));
            if (usesFunctions)
            {
                w.Line("/// @ignore");
                w.Function($"__{c.Name}_get_dispatcher", [], funcBody =>
                {
                    funcBody.Assign("__dispatcher", $"new {extCoreFunctionDispatcher}(__{c.Name}_invocation_handler, __{c.Name}_get_decoders())", VariableScope.Static);
                    funcBody.Return("__dispatcher");
                });
            }
        }

        private static void EmitConstant(IrConstant cst, GmlWriter w)
        {
            w.Line($"#macro {cst.Name} {cst.Literal}");
        }

        private static void EmitEnum(IrEnum e, GmlWriter w)
        {
            w.Enum(e.Name, e.Members.Select(m => new EnumMember(m.Name, m.DefaultLiteral)));
        }

        // ---------- struct code ----------
        private static void EmitStruct(GmlEmitterContext ctx, IrStruct s, GmlWriter w)
        {
            w.Struct(s.Name, structBody => {

                structBody.Assign("__uid", StringHash.ToUInt32(s.Name).ToString(), VariableScope.Static).Line();

                // field assignments
                foreach (var f in s.Fields)
                {
                    structBody.Assign(f.Name, f.Value ?? "undefined");
                }
            });
        }

        private static void EmitEncoder(GmlEmitterContext ctx, IrStruct s, GmlWriter w) 
        {
            var bufferName = "_buffer";

            w.JsDoc(builder => {
                builder.Line($"@func __{s.Name}_encode(_inst, {bufferName}, _offset, _where)");
                builder.Param(new ParamDoc("_inst", $"Struct.{s.Name}"));
                builder.Param(new ParamDoc(bufferName, "Id.Buffer"));
                builder.Param(new ParamDoc("_offset", "Real"));
                builder.Param(new ParamDoc("_where", "String"));
                builder.Tag("ignore");
            });
            w.Function($"__{s.Name}_encode", ["_inst", bufferName, "_offset", "_where = _GMFUNCTION_"], funcBody =>
            {
                funcBody.Call("buffer_seek", bufferName, "buffer_seek_start", "_offset").Line(";");

                w.With("_inst", withBody => 
                {
                    foreach (var f in s.Fields)
                    {
                        funcBody.Comment($"field: {f.Name}, type: {f.Type.Name}{(f.Type.IsCollection ? $"[{f.Type.FixedLength}]" : string.Empty)}");
                        WriteExpr(ctx, funcBody, $"{f.Name}", f.Type);
                        funcBody.Line();
                    }
                });
            });
        }

        private static void EmitDecoder(GmlEmitterContext ctx, IrStruct s, GmlWriter w) 
        {
            var bufferName = "_buffer";

            var usesDynamic = s.Fields.Any(f => f.Type.Kind is IrTypeKind.AnyArray or IrTypeKind.AnyMap);

            w.JsDoc(builder => {
                builder.Line($"@func __{s.Name}_decode({bufferName}, _offset)");
                builder.Param(new ParamDoc(bufferName, "Id.Buffer"));
                builder.Param(new ParamDoc("_offset", "Real"));
                builder.Returns($"Struct.{s.Name}");
                builder.Tag("ignore");
            });
            w.Function($"__{s.Name}_decode", [bufferName, "_offset"], funcBody => 
            {
                if (usesDynamic)
                {
                    funcBody.Assign("__decoders", $"__{ctx.ExtName}_get_decoders()", VariableScope.Static).Line();
                }

                funcBody.Call("buffer_seek", bufferName, "buffer_seek_start", "_offset").Line(";");

                funcBody.Line();
                funcBody.Assign("_inst", $"new {s.Name}()");

                funcBody.With("_inst", withBody => 
                {
                    foreach (var f in s.Fields)
                    {
                        funcBody.Comment($"field: {f.Name}, type: {f.Type.Name}{(f.Type.IsCollection ? $"[{f.Type.FixedLength}]" : string.Empty)}");
                        ReadExpr(funcBody, $"{f.Name}", f.Type);
                        funcBody.Line();
                    }
                });

                funcBody.Line();
                funcBody.Return("_inst");
            });
        }

        // ---------- function wrappers ----------
        private static void EmitWrapper(GmlEmitterContext ctx, IrCompilation c, IrFunction fn, GmlWriter w)
        {
            bool needArgsBuf = IrAnalysis.NeedsArgsBuffer(fn);
            bool needRetBuf = IrAnalysis.NeedsRetBuffer(fn);
            var direct = needArgsBuf ? [] : IrAnalysis.DirectArgs(fn).ToArray();

            var usesDynamic = fn.ReturnType.Kind is IrTypeKind.AnyArray or IrTypeKind.AnyMap;

            var usesFunctions = fn.Parameters.Any(p => p.Type.Kind == IrTypeKind.Function);

            if (!needArgsBuf && !needRetBuf) 
            {
                w.Comment($"Skipping function {fn.Name} (no wrapper is required)")
                .Line();
                return;
            }

            w.JsDoc(builder => {
                foreach (var p in fn.Parameters)
                {
                    builder.Param(new ParamDoc($"_{p.Name}", DocEmitter.JsDocType(p.Type)));
                }
                if (fn.ReturnType.Kind != IrTypeKind.Void)
                    builder.Returns(DocEmitter.JsDocType(fn.ReturnType));
            });
            w.Function(fn.Name, fn.Parameters.Select(p => $"_{p.Name}"), funcBody =>
            {
                if (usesFunctions) {
                    funcBody.Assign("__dispatcher", $"__{c.Name}_get_dispatcher()", VariableScope.Static).Line();
                }

                if (usesDynamic)
                {
                    funcBody.Assign("__decoders", $"__{c.Name}_get_decoders()", VariableScope.Static).Line();
                }

                // --- encode params ---
                if (needArgsBuf)
                {
                    funcBody.Assign(internalArgBuffer, expr => expr.Call(extCoreArgsBuffer, []), VariableScope.Local)
                    .Line();

                    foreach (var p in fn.Parameters)
                    {
                        funcBody.Comment($"param: _{p.Name}, type: {p.Type.Name}{(p.Type.IsCollection ? $"[{p.Type.FixedLength}]" : string.Empty)}");
                        WriteExpr(ctx, funcBody, $"_{p.Name}", p.Type, internalArgBuffer, "_GMFUNCTION_");
                        funcBody.Line();
                    }
                }

                // --- prepare ret buffer ---
                if (needRetBuf)
                {
                    funcBody.Assign(internalRetBuffer, expr => expr.Call(extCoreRetBuffer, Array.Empty<string>()), VariableScope.Local)
                    .Line();
                }

                // --- external call ---
                List<string> args = [];
                foreach (var p in direct) args.Add($"_{p.Name}");
                if (needArgsBuf) args.AddRange([$"buffer_get_address({internalArgBuffer})", $"buffer_tell({internalArgBuffer})"]);
                if (needRetBuf) args.AddRange([$"buffer_get_address({internalRetBuffer})", $"buffer_get_size({internalRetBuffer})"]);

                funcBody.Assign("_return_value", expr => expr.Call($"__{fn.Name}", [.. args]), VariableScope.Local)
                    .Line();
                
                // --- decode result ---
                if (needRetBuf)
                {
                    funcBody.Assign("_result", "undefined", VariableScope.Local);
                    ReadExpr(funcBody, "_result", fn.ReturnType, internalRetBuffer);

                    funcBody.Line("return _result;");
                }
                else
                {
                    funcBody.Line("return _return_value;");
                }
            });
        }

        // ---------- helpers ----------
        private static void WriteExpr(GmlEmitterContext ctx, GmlWriter w, string id, IrType t, string buf = "_buffer", string where = "_where")
        {
            // optional -> bool + nested write
            if (t.IsNullable)
            {
                WriteOptional(ctx, w, id, t, buf, where);
                return;
            }

            // arrays / vectors
            if (t.IsCollection)
            {
                w.Line(EmitCheck(id, t, where));
                if (t.FixedLength is null)
                {
                    w.Assign("_length", expr => expr.Call("array_length", id), VariableScope.Local);
                    w.Call("buffer_write", buf, "buffer_u32", "_length").Line(";");
                }
                w.For("var _i = 0", $"_i < {(t.FixedLength.HasValue ? t.FixedLength : "_length")}", "++_i", forBody => {
                    WriteExpr(ctx, forBody, $"{id}[_i]", Element(t), buf, where);
                });
                return;
            }

            switch (t.Kind)
            {
                case IrTypeKind.Scalar:
                    w.Line(EmitCheck(id, t, where));
                    if (t.IsStringScalar)
                    {
                        w.Call("buffer_write", buf, "buffer_u32", $"string_length({id})").Line(";");
                    }
                    w.Call("buffer_write", buf, GmlBufCode(t), id).Line(";");
                    break;

                case IrTypeKind.Struct:
                    w.Line(EmitCheck(id, t, where));
                    w.Call($"__{t.Name}_encode", id, buf, $"buffer_tell({buf})", where).Line(";");
                    break;

                case IrTypeKind.Buffer:
                    w.Line(EmitCheck(id, t, where));
                    w.Call($"__{ctx.ExtName}_queue_buffer", $"buffer_get_address({id})", $"buffer_get_size({id})").Line(";");
                    break;

                case IrTypeKind.Enum:
                    w.Line(EmitCheck(id, t.Underlying!, where));
                    w.Call("buffer_write", buf, GmlBufCode(t.Underlying!), id).Line(";");
                    break;

                case IrTypeKind.Function:
                    w.Line(EmitCheck(id, t, where));
                    // Functions should register themselves into the extension core system
                    w.Assign($"{id}_handle", expr => expr.Call(extCoreFunctionRegister, id, "__dispatcher"), VariableScope.Local);
                    w.Call("buffer_write", buf, GmlBufCode(t), $"{id}_handle").Line(";");
                    break;

                case IrTypeKind.AnyArray:
                case IrTypeKind.AnyMap:
                case IrTypeKind.Any:
                    w.Call(extCoreMarshalValue, buf, id).Line(";");
                    break;

                default:
                    throw new NotSupportedException($"GML emitter: requested type ({t.Name}) is not supported yet.");
            }
        }

        private static void ReadExpr(GmlWriter w, string id, IrType t, string buf = "_buffer")
        {
            // optional -> bool + nested read
            if (t.IsNullable)
            {
                ReadOptional(w, id, t, buf);
                return;
            }

            // arrays / vectors
            if (t.IsCollection)
            {
                if (t.FixedLength is null)
                {
                    w.Assign("_length", expr => expr.Call("buffer_read", buf, "buffer_u32"), VariableScope.Local);
                }
                var length = t.FixedLength.HasValue ? t.FixedLength.ToString() : "_length";

                w.Assign(id, expr => expr.Call("array_create", length!));
                w.For("var _i = 0", $"_i < {length}", "++_i", forBody => {
                    ReadExpr(forBody, $"{id}[_i]", Element(t), buf);
                });
                return;
            }

            if (t.IsStringScalar) 
            {
                w.Call("buffer_read", buf, "buffer_u32").Line(";");
            }

            switch (t.Kind)
            {
                case IrTypeKind.Scalar:
                    w.Assign(id, expr => expr.Call("buffer_read", buf, GmlBufCode(t)));
                    break;

                case IrTypeKind.Enum:
                    w.Assign(id, expr => expr.Call("buffer_read", buf, GmlBufCode(t.Underlying!)));
                    break;

                case IrTypeKind.Struct:
                    w.Assign(id, expr => expr.Call($"__{t.Name}_decode", buf, $"buffer_tell({buf})"));
                    break;

                case IrTypeKind.AnyArray:
                case IrTypeKind.AnyMap:
                    w.Assign(id, expr => expr.Call(extCoreUnmarshalValue, buf, "__decoders"));
                    break;

                default:
                    throw new NotSupportedException($"GML emitter: requested type ({t.Name}) is not supported yet.");
            }
        }

        // ---------- optional helpers ----------
        private static void WriteOptional(GmlEmitterContext ctx, GmlWriter w, string id, IrType t, string buf, string where)
        {
            var inner = t with { IsNullable = false, };

            w.If($"is_undefined({id})", thenBody =>
            {
                thenBody.Call("buffer_write", buf, "buffer_bool", "false").Line(";");
            }, elseBody => {
                elseBody.Call("buffer_write", buf, "buffer_bool", "true").Line(";");
                WriteExpr(ctx,elseBody, id, inner, buf, where);
            });
        }

        private static void ReadOptional(GmlWriter w, string id, IrType t, string buf)
        {
            var inner = t with { IsNullable = false, };

            w.If($"buffer_read({buf}, buffer_bool)", thenBody =>
            {
                ReadExpr(thenBody, id, inner, buf);
            }, elseBody => {
                elseBody.Assign(id, "undefined");
            });
        }

        // ---------- misc ----------
        private static string EmitCheck(string id, IrType t, string where = "_where")
        {
            return t.Kind switch
            {
                _ when t.IsCollection => t.FixedLength is null ? 
                    // Unknown size (allow any size)
                    $"if (!is_array({id})) show_error($\"{{{where}}} :: {id} expected array\", true);" :
                    // Known size (needs to be of given size)
                    $"if (!is_array({id}) || (array_length({id}) != {t.FixedLength})) show_error($\"{{{where}}} :: {id} expected array (size {t.FixedLength})\", true);",

                IrTypeKind.Scalar when t.Name == "string" =>
                    $"if (!is_string({id})) show_error($\"{{{where}}} :: {id} expected string\", true);",

                IrTypeKind.Scalar when t.Name == "bool" =>
                    $"if (!is_bool({id})) show_error($\"{{{where}}} :: {id} expected bool\", true);",

                IrTypeKind.Scalar =>
                    $"if (!is_numeric({id})) show_error($\"{{{where}}} :: {id} expected number\", true);",

                IrTypeKind.Buffer =>
                    $"if (!buffer_exists({id})) show_error($\"{{{where}}} :: {id} expected Id.Buffer\", true);",

                IrTypeKind.Function =>
                    $"if (!is_callable({id})) show_error($\"{{{where}}} :: {id} expected callable type\", true);",

                IrTypeKind.Struct =>
                    $"if ({id}.__uid != {StringHash.ToUInt32(t.Name)}) show_error($\"{{{where}}} :: {id} expected {t.Name}\", true);",

                _ => ""
            };
        }

        private static string GmlBufCode(IrType t) => t.Name switch
        {
            "int64" => "buffer_u64",
            "uint64" => "buffer_u64",
            "int32" => "buffer_s32",
            "uint32" => "buffer_u32",
            "int16" => "buffer_s16",
            "uint16" => "buffer_u16",
            "int8" => "buffer_s8",
            "uint8" => "buffer_u8",
            "bool" => "buffer_bool",
            "float" => "buffer_f32",
            "double" => "buffer_f64",
            "string" => "buffer_string",  // handled via length+string anyway
            "func" => "buffer_u64",   // handled as the function unique ptr
            "function" => "buffer_u64",   // handled as the function unique ptr
            "buffer" => "buffer_u64",
            _ => "buffer_f64"
        };

        private static IrType Element(IrType t) => t with { IsCollection = false, FixedLength = null };

    }
}
