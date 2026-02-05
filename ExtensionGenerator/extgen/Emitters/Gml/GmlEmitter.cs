using codegencore.Model;
using codegencore.Writers;
using codegencore.Writers.JSDoc;
using codegencore.Writers.Lang;
using extgen.Emitters.Doc;
using extgen.Model;
using extgen.Model.Utils;
using extgen.Options;
using extgen.Utils;
using extgencore.Helpers;
using System.Collections.Immutable;

namespace extgen.Emitters.Gml
{
    public sealed class GmlEmitter(GmlEmitterOptions options) : IIrEmitter
    {
        private readonly GmlEmitterOptions _options = options ?? throw new ArgumentNullException(nameof(options));

        private const string InternalArgBuffer = "__args_buffer";
        private const string InternalRetBuffer = "__ret_buffer";

        private const string ExtCoreArgsBuffer = "__ext_core_get_args_buffer";
        private const string ExtCoreRetBuffer = "__ext_core_get_ret_buffer";
        private const string ExtCoreMarshalValue = "__ext_core_buffer_marshal_value";
        private const string ExtCoreUnmarshalValue = "__ext_core_buffer_unmarshal_value";
        private const string ExtCoreFunctionRegister = "__ext_core_function_register";
        private const string ExtCoreFunctionDispatcher = "__GMNativeFunctionDispatcher";

        public void Emit(IrCompilation comp, string dir)
        {
            var ctx = new GmlEmitterContext(comp.Name, _options);
            var layout = new GmlLayout(dir, _options);

            var enums = new IrTypeEnumResolver(comp.Enums);



            FileEmitHelpers.WriteGml(layout.OutputFolder, $"{layout.OutputFile}.gml", w => EmitAll(ctx, comp, enums, w));
        }

        // ============================================================
        // Top-level emission
        // ============================================================

        private static void EmitAll(GmlEmitterContext ctx, IrCompilation c, IIrTypeEnumResolver enums, GmlWriter w)
        {
            w.Comment("Auto-generated – do not edit");

            EmitMacros(c.Constants, w);
            EmitEnums(c.Enums, w);
            EmitConstructors(ctx, c.Structs, w);
            EmitCodecs(ctx, c.Structs, enums, w);
            EmitFunctions(ctx, c.Functions, enums, w);

            EmitHandlerRegistration(c, w);
        }

        // ============================================================
        // Higher level
        // ============================================================

        private static void EmitFunctions(GmlEmitterContext ctx, ImmutableArray<IrFunction> funcs, IIrTypeEnumResolver enums, GmlWriter w)
        {
            w.Section("Functions").Line();
            foreach (var fn in funcs)
            {
                EmitWrapper(ctx, enums, fn, w);
                w.Line();
            }
        }

        private static void EmitCodecs(GmlEmitterContext ctx, ImmutableArray<IrStruct> structs, IIrTypeEnumResolver enums, GmlWriter w)
        {
            w.Section("Codecs").Line();
            foreach (var s in structs)
            {
                EmitEncoder(ctx, enums, s, w);
                w.Line();
                EmitDecoder(ctx, enums, s, w);
                w.Line();
            }
        }

        private static void EmitConstructors(GmlEmitterContext ctx, ImmutableArray<IrStruct> structs, GmlWriter w)
        {
            w.Section("Constructors").Line();
            foreach (var s in structs)
            {
                EmitStruct(ctx, s, w);
                w.Line();
            }
        }

        private static void EmitEnums(ImmutableArray<IrEnum> enums, GmlWriter w)
        {
            w.Section("Enums").Line();
            foreach (var e in enums)
            {
                EmitEnum(e, w);
                w.Line();
            }
        }

        private static void EmitMacros(ImmutableArray<IrConstant> constants, GmlWriter w)
        {
            w.Section("Macros").Line();
            foreach (var cst in constants)
            {
                EmitConstant(cst, w);
                w.Line();
            }
        }

        private static void EmitHandlerRegistration(IrCompilation c, GmlWriter w)
        {
            // decoder registry
            w.Line("/// @ignore");
            w.Function($"__{c.Name}_get_decoders", [], funcBody =>
            {
                funcBody.Assign("__decoders",
                    expr => expr.ArrayLiteral(c.Structs.Select(s => $"__{s.Name}_decode"), true),
                    VariableScope.Static);
                funcBody.Return("__decoders");
            });

            // dispatcher (only if any param uses Function)
            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
            if (usesFunctions)
            {
                w.Line("/// @ignore");
                w.Function($"__{c.Name}_get_dispatcher", [], funcBody =>
                {
                    funcBody.Assign("__dispatcher",
                        $"new {ExtCoreFunctionDispatcher}(__{c.Name}_invocation_handler, __{c.Name}_get_decoders())",
                        VariableScope.Static);
                    funcBody.Return("__dispatcher");
                });
            }
        }

        // ============================================================
        // Lower level
        // ============================================================

        private static void EmitConstant(IrConstant cst, GmlWriter w)
            => w.Line($"#macro {cst.Name} {cst.Literal}");

        private static void EmitEnum(IrEnum e, GmlWriter w)
            => w.Enum(e.Name, e.Members.Select(m => new EnumMember(m.Name, m.DefaultLiteral)));

        private static void EmitStruct(GmlEmitterContext ctx, IrStruct s, GmlWriter w)
        {
            w.Struct(s.Name, body =>
            {
                body.Assign("__uid", StringHash.ToUInt32(s.Name).ToString(), VariableScope.Static).Line();

                foreach (var f in s.Fields)
                    body.Assign(f.Name, f.Value ?? "undefined");
            });
        }

        private static void EmitEncoder(GmlEmitterContext ctx, IIrTypeEnumResolver enums, IrStruct s, GmlWriter w)
        {
            const string bufferName = "_buffer";

            w.JsDoc(builder =>
            {
                builder.Line($"@func __{s.Name}_encode(_inst, {bufferName}, _offset, _where)");
                builder.Param(new ParamDoc("_inst", $"Struct.{s.Name}"));
                builder.Param(new ParamDoc(bufferName, "Id.Buffer"));
                builder.Param(new ParamDoc("_offset", "Real"));
                builder.Param(new ParamDoc("_where", "String"));
                builder.Tag("ignore");
            });

            w.Function($"__{s.Name}_encode", ["_inst", bufferName, "_offset", "_where = _GMFUNCTION_"], fn =>
            {
                fn.Call("buffer_seek", bufferName, "buffer_seek_start", "_offset").Line(";");

                fn.With("_inst", withBody =>
                {
                    foreach (var f in s.Fields)
                    {
                        fn.Comment($"field: {f.Name}, type: {f.Type.ToDebugString()}");
                        WriteValue(ctx, enums, fn, f.Name, f.Type, bufferName, "_where");
                        fn.Line();
                    }
                });
            });
        }

        private static void EmitDecoder(GmlEmitterContext ctx, IIrTypeEnumResolver enums, IrStruct s, GmlWriter w)
        {
            const string bufferName = "_buffer";

            var usesDynamic = s.Fields.Any(p =>
                p.Type.ContainsBuiltin(BuiltinKind.AnyArray) ||
                p.Type.ContainsBuiltin(BuiltinKind.AnyMap));

            w.JsDoc(builder =>
            {
                builder.Line($"@func __{s.Name}_decode({bufferName}, _offset)");
                builder.Param(new ParamDoc(bufferName, "Id.Buffer"));
                builder.Param(new ParamDoc("_offset", "Real"));
                builder.Returns($"Struct.{s.Name}");
                builder.Tag("ignore");
            });

            w.Function($"__{s.Name}_decode", [bufferName, "_offset"], fn =>
            {
                if (usesDynamic)
                    fn.Assign("__decoders", $"__{ctx.ExtName}_get_decoders()", VariableScope.Static).Line();

                fn.Call("buffer_seek", bufferName, "buffer_seek_start", "_offset").Line(";");

                fn.Line();
                fn.Assign("_inst", $"new {s.Name}()");

                fn.With("_inst", withBody =>
                {
                    foreach (var f in s.Fields)
                    {
                        fn.Comment($"field: {f.Name}, type: {f.Type.ToDebugString()}");
                        ReadValue(enums, fn, f.Name, f.Type, bufferName);
                        fn.Line();
                    }
                });

                fn.Line();
                fn.Return("_inst");
            });
        }

        private static void EmitWrapper(GmlEmitterContext ctx, IIrTypeEnumResolver enums, IrFunction fn, GmlWriter w)
        {
            bool needArgsBuf = IrAnalysis.NeedsArgsBuffer(fn);
            bool needRetBuf = IrAnalysis.NeedsRetBuffer(fn);

            // direct args are those passed without serialization
            var direct = needArgsBuf ? [] : IrAnalysis.DirectArgs(fn).ToArray();

            var usesDynamic = fn.Parameters.Any(p =>
                p.Type.ContainsBuiltin(BuiltinKind.AnyArray) ||
                p.Type.ContainsBuiltin(BuiltinKind.AnyMap));

            var usesFunctions = fn.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function));

            if (!needArgsBuf && !needRetBuf)
            {
                w.Comment($"Skipping function {fn.Name} (no wrapper is required)").Line();
                return;
            }

            w.JsDoc(builder =>
            {
                foreach (var p in fn.Parameters)
                    builder.Param(new ParamDoc($"_{p.Name}", DocEmitter.JsDocType(p.Type)));

                if (!IrTypeUtil.IsVoid(fn.ReturnType))
                    builder.Returns(DocEmitter.JsDocType(fn.ReturnType));
            });

            w.Function(fn.Name, fn.Parameters.Select(p => $"_{p.Name}"), body =>
            {
                if (usesFunctions)
                    body.Assign("__dispatcher", $"__{ctx.ExtName}_get_dispatcher()", VariableScope.Static).Line();

                if (usesDynamic)
                    body.Assign("__decoders", $"__{ctx.ExtName}_get_decoders()", VariableScope.Static).Line();

                // --- encode params ---
                if (needArgsBuf)
                {
                    body.Assign(InternalArgBuffer, expr => expr.Call(ExtCoreArgsBuffer, []), VariableScope.Local).Line();

                    foreach (var p in fn.Parameters)
                    {
                        body.Comment($"param: _{p.Name}, type: {p.Type.ToDebugString()}");
                        WriteValue(ctx, enums, body, $"_{p.Name}", p.Type, InternalArgBuffer, "_GMFUNCTION_");
                        body.Line();
                    }
                }

                // --- prepare ret buffer ---
                if (needRetBuf)
                    body.Assign(InternalRetBuffer, expr => expr.Call(ExtCoreRetBuffer, Array.Empty<string>()), VariableScope.Local).Line();

                // --- external call ---
                List<string> args = [];

                foreach (var p in direct) args.Add($"_{p.Name}");

                if (needArgsBuf)
                    args.AddRange([$"buffer_get_address({InternalArgBuffer})", $"buffer_tell({InternalArgBuffer})"]);

                if (needRetBuf)
                    args.AddRange([$"buffer_get_address({InternalRetBuffer})", $"buffer_get_size({InternalRetBuffer})"]);

                body.Assign("_return_value", expr => expr.Call($"__{fn.Name}", [.. args]), VariableScope.Local).Line();

                // --- decode result ---
                if (needRetBuf)
                {
                    body.Assign("_result", "undefined", VariableScope.Local);
                    ReadValue(enums, body, "_result", fn.ReturnType, InternalRetBuffer);
                    body.Line("return _result;");
                }
                else
                {
                    body.Line("return _return_value;");
                }
            });
        }

        // ============================================================
        // Encode / Decode helpers
        // ============================================================

        private static void WriteValue(GmlEmitterContext ctx, IIrTypeEnumResolver enums, GmlWriter w, string id, IrType t, string buf, string where)
        {
            // Nullable: presence bool + payload
            if (t is IrType.Nullable nn)
            {
                WriteOptional(ctx, enums, w, id, nn.Underlying, buf, where);
                return;
            }

            // Array: length (if dynamic) + elements
            if (t is IrType.Array arr)
            {
                w.Line(EmitCheck(id, t, where));

                if (arr.FixedLength is null)
                {
                    w.Assign("_length", expr => expr.Call("array_length", id), VariableScope.Local);
                    w.Call("buffer_write", buf, "buffer_u32", "_length").Line(";");
                }

                var lenExpr = arr.FixedLength.HasValue ? arr.FixedLength.Value.ToString() : "_length";
                w.For("var _i = 0", $"_i < {lenExpr}", "++_i", forBody =>
                {
                    WriteValue(ctx, enums, forBody, $"{id}[_i]", arr.Element, buf, where);
                });
                return;
            }

            // Atomic
            w.Line(EmitCheck(id, t, where));

            switch (t)
            {
                case IrType.Builtin b:
                    WriteBuiltin(ctx, w, id, b.Kind, buf, where);
                    return;

                case IrType.Named { Kind: NamedKind.Struct, Name: var structName }:
                    w.Call($"__{structName}_encode", id, buf, $"buffer_tell({buf})", where).Line(";");
                    return;

                case IrType.Named { Kind: NamedKind.Enum, Name: var enumName }:
                    {
                        var underlying = enums.GetUnderlying(enumName);
                        w.Line(EmitCheck(id, underlying, where));
                        w.Call("buffer_write", buf, GmlBufCode(underlying), id).Line(";");
                        return;
                    }

                default:
                    throw new NotSupportedException($"GML emitter: write type ({t.ToDebugString()}) not supported.");
            }
        }

        private static void WriteBuiltin(GmlEmitterContext ctx, GmlWriter w, string id, BuiltinKind kind, string buf, string where)
        {
            switch (kind)
            {
                case BuiltinKind.String:
                    // write length + payload (you already do this)
                    w.Call("buffer_write", buf, "buffer_u32", $"string_length({id})").Line(";");
                    w.Call("buffer_write", buf, GmlBufCode(new IrType.Builtin(BuiltinKind.String)), id).Line(";");
                    return;

                case BuiltinKind.Buffer:
                    w.Call($"__{ctx.ExtName}_queue_buffer", $"buffer_get_address({id})", $"buffer_get_size({id})").Line(";");
                    return;

                case BuiltinKind.Function:
                    w.Assign($"{id}_handle", expr => expr.Call(ExtCoreFunctionRegister, id, "__dispatcher"), VariableScope.Local);
                    w.Call("buffer_write", buf, "buffer_u64", $"{id}_handle").Line(";");
                    return;

                case BuiltinKind.Any:
                case BuiltinKind.AnyArray:
                case BuiltinKind.AnyMap:
                    w.Call(ExtCoreMarshalValue, buf, id).Line(";");
                    return;

                // numeric/bool
                case BuiltinKind.Bool:
                case BuiltinKind.Int8:
                case BuiltinKind.UInt8:
                case BuiltinKind.Int16:
                case BuiltinKind.UInt16:
                case BuiltinKind.Int32:
                case BuiltinKind.UInt32:
                case BuiltinKind.Int64:
                case BuiltinKind.UInt64:
                case BuiltinKind.Float32:
                case BuiltinKind.Float64:
                    w.Call("buffer_write", buf, GmlBufCode(new IrType.Builtin(kind)), id).Line(";");
                    return;

                case BuiltinKind.Void:
                    // nothing to write
                    return;

                default:
                    throw new NotSupportedException($"GML emitter: builtin write kind {kind} not supported.");
            }
        }

        private static void ReadValue(IIrTypeEnumResolver enums, GmlWriter w, string id, IrType t, string buf)
        {
            // Nullable: presence bool + payload
            if (t is IrType.Nullable nn)
            {
                ReadOptional(enums, w, id, nn.Underlying, buf);
                return;
            }

            // Array: length (if dynamic) + elements
            if (t is IrType.Array arr)
            {
                if (arr.FixedLength is null)
                    w.Assign("_length", expr => expr.Call("buffer_read", buf, "buffer_u32"), VariableScope.Local);

                var lenExpr = arr.FixedLength.HasValue ? arr.FixedLength.Value.ToString() : "_length";

                w.Assign(id, expr => expr.Call("array_create", lenExpr));
                w.For("var _i = 0", $"_i < {lenExpr}", "++_i", forBody =>
                {
                    ReadValue(enums, forBody, $"{id}[_i]", arr.Element, buf);
                });
                return;
            }

            // Atomic
            switch (t)
            {
                case IrType.Builtin b:
                    ReadBuiltin(w, id, b.Kind, buf);
                    return;

                case IrType.Named { Kind: NamedKind.Enum, Name: var enumName }:
                    {
                        var underlying = enums.GetUnderlying(enumName);
                        w.Assign(id, expr => expr.Call("buffer_read", buf, GmlBufCode(underlying)));
                        return;
                    }

                case IrType.Named { Kind: NamedKind.Struct, Name: var structName }:
                    w.Assign(id, expr => expr.Call($"__{structName}_decode", buf, $"buffer_tell({buf})"));
                    return;

                default:
                    throw new NotSupportedException($"GML emitter: read type ({t.ToDebugString()}) not supported.");
            }
        }

        private static void ReadBuiltin(GmlWriter w, string id, BuiltinKind kind, string buf)
        {
            // your string layout: skip length then read string
            if (kind == BuiltinKind.String)
                w.Call("buffer_read", buf, "buffer_u32").Line(";");

            switch (kind)
            {
                case BuiltinKind.Any:
                case BuiltinKind.AnyArray:
                case BuiltinKind.AnyMap:
                    w.Assign(id, expr => expr.Call(ExtCoreUnmarshalValue, buf, "__decoders"));
                    return;

                case BuiltinKind.Bool:
                case BuiltinKind.Int8:
                case BuiltinKind.UInt8:
                case BuiltinKind.Int16:
                case BuiltinKind.UInt16:
                case BuiltinKind.Int32:
                case BuiltinKind.UInt32:
                case BuiltinKind.Int64:
                case BuiltinKind.UInt64:
                case BuiltinKind.Float32:
                case BuiltinKind.Float64:
                case BuiltinKind.String:
                    w.Assign(id, expr => expr.Call("buffer_read", buf, GmlBufCode(new IrType.Builtin(kind))));
                    return;

                // Buffer/function are not read directly from ret buffer in your current design
                // (buffers are queued; functions come through dispatcher system).
                default:
                    throw new NotSupportedException($"GML emitter: builtin read kind {kind} not supported.");
            }
        }

        // ============================================================
        // Optional helpers
        // ============================================================

        private static void WriteOptional(GmlEmitterContext ctx, IIrTypeEnumResolver enums, GmlWriter w, string id, IrType inner, string buf, string where)
        {
            w.If($"is_undefined({id})", thenBody =>
            {
                thenBody.Call("buffer_write", buf, "buffer_bool", "false").Line(";");
            }, elseBody =>
            {
                elseBody.Call("buffer_write", buf, "buffer_bool", "true").Line(";");
                WriteValue(ctx, enums, elseBody, id, inner, buf, where);
            });
        }

        private static void ReadOptional(IIrTypeEnumResolver enums, GmlWriter w, string id, IrType inner, string buf)
        {
            w.If($"buffer_read({buf}, buffer_bool)", thenBody =>
            {
                ReadValue(enums, thenBody, id, inner, buf);
            }, elseBody =>
            {
                elseBody.Assign(id, "undefined");
            });
        }

        // ============================================================
        // Validation + buffer code mapping
        // ============================================================

        private static string EmitCheck(string id, IrType t, string where = "_where")
        {
            // Arrays
            if (t is IrType.Array a)
            {
                return a.FixedLength is null
                    ? $"if (!is_array({id})) show_error($\"{{{where}}} :: {id} expected array\", true);"
                    : $"if (!is_array({id}) || (array_length({id}) != {a.FixedLength})) show_error($\"{{{where}}} :: {id} expected array (size {a.FixedLength})\", true);";
            }

            // Nullable is checked at the optional wrapper level (undefined/present)
            if (t is IrType.Nullable)
                return string.Empty;

            // Builtins / Named
            return t switch
            {
                IrType.Builtin { Kind: BuiltinKind.String } =>
                    $"if (!is_string({id})) show_error($\"{{{where}}} :: {id} expected string\", true);",

                IrType.Builtin { Kind: BuiltinKind.Bool } =>
                    $"if (!is_bool({id})) show_error($\"{{{where}}} :: {id} expected bool\", true);",

                IrType.Builtin { Kind: BuiltinKind.Buffer } =>
                    $"if (!buffer_exists({id})) show_error($\"{{{where}}} :: {id} expected Id.Buffer\", true);",

                IrType.Builtin { Kind: BuiltinKind.Function } =>
                    $"if (!is_callable({id})) show_error($\"{{{where}}} :: {id} expected callable type\", true);",

                // numeric builtins
                IrType.Builtin
                {
                    Kind: BuiltinKind.Int8 or BuiltinKind.UInt8
                                   or BuiltinKind.Int16 or BuiltinKind.UInt16
                                   or BuiltinKind.Int32 or BuiltinKind.UInt32
                                   or BuiltinKind.Int64 or BuiltinKind.UInt64
                                   or BuiltinKind.Float32 or BuiltinKind.Float64
                } =>
                    $"if (!is_numeric({id})) show_error($\"{{{where}}} :: {id} expected number\", true);",

                // Any* are marshaled by core; accept anything
                IrType.Builtin { Kind: BuiltinKind.Any or BuiltinKind.AnyArray or BuiltinKind.AnyMap } =>
                    string.Empty,

                IrType.Named { Kind: NamedKind.Struct, Name: var name } =>
                    $"if ({id}.__uid != {StringHash.ToUInt32(name)}) show_error($\"{{{where}}} :: {id} expected {name}\", true);",

                // Enums in your GML surface are usually strings or numbers depending on your design.
                // Here we DON'T enforce enum literals, just type correctness in WriteValue for underlying.
                IrType.Named { Kind: NamedKind.Enum } =>
                    string.Empty,

                _ => string.Empty
            };
        }

        private static string GmlBufCode(IrType t)
        {
            // only builtins (and enum underlying which must be builtin)
            if (t is not IrType.Builtin b)
                throw new NotSupportedException($"GML buffer code: expected builtin type, got {t.ToDebugString()}");

            return b.Kind switch
            {
                BuiltinKind.Int64 or BuiltinKind.UInt64 => "buffer_u64",

                BuiltinKind.Int32 => "buffer_s32",
                BuiltinKind.UInt32 => "buffer_u32",

                BuiltinKind.Int16 => "buffer_s16",
                BuiltinKind.UInt16 => "buffer_u16",

                BuiltinKind.Int8 => "buffer_s8",
                BuiltinKind.UInt8 => "buffer_u8",

                BuiltinKind.Bool => "buffer_bool",
                BuiltinKind.Float32 => "buffer_f32",
                BuiltinKind.Float64 => "buffer_f64",

                // You do length+payload yourself, but you still call buffer_write/read with this:
                BuiltinKind.String => "buffer_string",

                // function handles and buffers are passed as u64 handles in your design
                BuiltinKind.Function => "buffer_u64",
                BuiltinKind.Buffer => "buffer_u64",

                _ => throw new NotSupportedException($"GML buffer code: builtin {b.Kind} not supported.")
            };
        }
    }
}
