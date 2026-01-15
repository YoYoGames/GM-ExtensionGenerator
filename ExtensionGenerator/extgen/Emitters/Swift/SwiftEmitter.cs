using codegencore.Writers.Concrete;
using codegencore.Writers.Lang;
using extgen.Bridge.Swift;
using extgen.Emitters.Objc;
using extgen.Emitters.Utils;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem.Cpp;
using extgen.TypeSystem.Swift;
using extgen.Utils;
using System.Collections.Immutable;
using System.Text;

namespace extgen.Emitters.Swift
{
    public sealed class SwiftEmitter(IObjcEmitterOptions options, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly SwiftTypeMap typeMap = new(runtime);

        public void Emit(IrCompilation comp, string dir)
        {
            ObjcEmitterContext ctx = new(comp.Name, options, runtime);
            ObjcLayout layout = new(dir, options);

            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(ObjcEmitterContext ctx, IrCompilation c, ObjcLayout layout)
        {
            CppTypeMap cppTypeMap = new(ctx.Runtime);
            
            // Swift flavor bridge on ObjC side
            SwiftBridge bridge = new();

            ObjcCommonEmitter common = new(ctx, cppTypeMap, bridge);
            common.EmitInternal(c, layout);
            common.EmitObjcUserShell(c, layout);

            // Swift base (always regenerated)
            FileEmitHelpers.WriteSwift(layout.CodeGenDir, $"{ctx.ExtName}Artifacts.swift", w => EmitArtifacts(w, c));
            FileEmitHelpers.WriteSwift(layout.CodeGenDir, $"{ctx.ExtName}InternalSwift.swift", w => EmitInternalSwift(ctx, c, w));

            // user Swift file (only if missing)
            FileEmitHelpers.WriteSwiftIfMissing(layout.SourceDir, $"{ctx.ExtName}Swift.swift", w => EmitUserSwift(ctx, c, w));
        }

        // =====================================================================
        // 1. SWIFT ARTIFACTS
        // =====================================================================

        private void EmitArtifacts(SwiftWriter w, IrCompilation c)
        {
            EmitConsts(w, c.Constants);
            EmitEnums(w, c.Enums);
            EmitStructs(w, c.Structs);
            EmitStructCodecs(w, c.Structs);
        }

        // ------------------- Constants -------------------

        private void EmitConsts(SwiftWriter w, ImmutableArray<IrConstant> constants)
        {
            foreach (var c in constants) {
                w.Let(c.Name, typeMap.Map(c.Type), c.Literal);
            }
        }

        // --------------------- Enums ---------------------

        private void EmitEnums(SwiftWriter w, IImmutableList<IrEnum> enums)
        {
            foreach (var e in enums)
            {
                // Underlying scalar, e.g. Int32
                var underlying = e.Underlying
                                  ?? throw new InvalidOperationException($"Enum {e.Name} has no underlying type.");
                var rawType = typeMap.MapScalar(underlying, owned: false);

                var members = e.Members.Select(m => new EnumMember(
                    m.Name,
                    m.DefaultLiteral,   // the numeric raw value as string, e.g. "0", "1"
                    Comment: null));

                w.Enum(e.Name, members, rawType, modifiers: ["public"]);
                w.Line();
            }
        }

        // --------------------- Structs ---------------------

        private void EmitStructs(SwiftWriter w, IImmutableList<IrStruct> structs)
        {
            foreach (var s in structs)
            {
                w.Struct(s.Name, modifiers: ["public"], inher: null, body: body =>
                {
                    foreach (var f in s.Fields)
                    {
                        var swiftType = typeMap.Map(f.Type, owned: true);
                        body.Var(f.Name, swiftType, init: null, modifiers: ["public"]);
                    }
                });

                w.Line();
            }
        }

        // --------------------- ITypedStruct codecs ---------------------

        private void EmitStructCodecs(SwiftWriter w, IImmutableList<IrStruct> structs)
        {
            int codecId = 0;

            SwiftWireHelpers wireHelpers = new(runtime, typeMap);

            foreach (var s in structs)
            {
                w.Extension(s.Name, body =>
                {
                    // static let codecID: UInt32 = N
                    body.Line($"public static let codecID: UInt32 = {codecId}");

                    body.Line();

                    // init<R: IByteReader>(_ r: inout R) throws
                    body.Line("public init<R: IByteReader>(_ r: inout R) throws");
                    body.Block(init =>
                    {
                        foreach (var f in s.Fields)
                        {
                            // Decode into self.field
                            wireHelpers.DecodeLines(init, f.Type, $"self.{f.Name}", declare: false, bufferVar: "r");
                        }
                    }, trailingNewLine: true);

                    body.Line();

                    // func encode<W: IByteWriter>(_ w: inout W) throws
                    body.Line("public func encode<W: IByteWriter>(_ w: inout W) throws");
                    body.Block(enc =>
                    {
                        foreach (var f in s.Fields)
                        {
                            wireHelpers.EncodeLines(enc, f.Type, $"self.{f.Name}", bufferVar: "w");
                        }
                    }, trailingNewLine: true);
                });

                w.Line();
                codecId++;
            }
        }


        // =====================================================================
        // 2. USER SWIFT STUB
        // =====================================================================

        private void EmitInternalSwift(ObjcEmitterContext ctx, IrCompilation c, SwiftWriter w)
        {
            var ext = c.Name;
            var runtime = ctx.Runtime;
            var typeMap = new SwiftTypeMap(runtime);

            // Imports
            w.Import("Foundation")
             .Import("os.log")
             .Import("CxxStdlib")
             .Line();

            var usesFunctions = c.Functions.Any(f => f.Parameters.Any(p => p.Type.Kind == IrTypeKind.Function));
            var usesBuffers = c.Functions.Any(f => f.Parameters.Any(p => p.Type.Kind == IrTypeKind.Buffer));

            // Base class that owns the wire entrypoints + open user-friendly API.
            w.Class(
                name: $"{ext}InternalSwift",
                modifiers: ["open"],
                inher: null,
                body: cls =>
                {
                    // ---------------------------------------------------
                    // 1) Internal fields (__dispatch_queue, __buffer_queue)
                    // ---------------------------------------------------
                    if (usesFunctions)
                    {
                        cls.Var(
                            name: runtime.DispatchQueueField,        // e.g. "__dispatch_queue"
                            type: "GMDispatchQueue",
                            init: "GMDispatchQueue()",
                            modifiers: ["internal"]);
                    }

                    if (usesBuffers)
                    {
                        cls.Var(
                            name: runtime.BufferQueueField,          // e.g. "__buffer_queue"
                            type: "[GMBuffer]",
                            init: "[]",
                            modifiers: ["internal"]);
                    }

                    cls.Line();

                    // public init() {}
                    cls.Init(
                        parameters: [],
                        modifiers: ["public"],
                        body: _ => { });

                    cls.Line();

                    // ---------------------------------------------------
                    // 2) Generated functions (user-facing + __EXT_SWIFT__)
                    // ---------------------------------------------------
                    EmitInternalSwiftFunctions(ctx, c.Functions, cls, typeMap);

                    // ---------------------------------------------------
                    // 3) Extra internal wire methods:
                    //    __EXT_SWIFT__ExtName_invocation_handler
                    //    __EXT_SWIFT__ExtName_queue_buffer
                    // ---------------------------------------------------
                    if (usesFunctions)
                    {
                        EmitSwiftInvocationHandler(ctx, cls);
                        cls.Line();
                    }

                    if (usesBuffers)
                    {
                        EmitSwiftQueueBuffer(ctx, cls);
                        cls.Line();
                    }

                });
        }

        private void EmitUserSwift(ObjcEmitterContext ctx, IrCompilation c, SwiftWriter w)
        {
            var ext = c.Name;

            w.Import("Foundation")
              .Import("CxxStdlib")
              .Line();

            w.Class($"{ext}Swift",
                modifiers: ["public"],
                inher: [$"{ext}InternalSwift"],
                body: cls =>
                {
                    cls.Init([], modifiers: ["public", "override"], body: _ =>
                    {
                        _.Line("super.init()");
                    });

                    foreach (var fn in c.Functions)
                    {
                        var ret = typeMap.Map(fn.ReturnType, false);
                        var ps = fn.Parameters.Select(p =>
                            new SwiftParam(
                                External: p.Name,
                                Internal: p.Name,
                                Type: typeMap.Map(p.Type, false))
                        );

                        cls.Func(fn.Name, ps, fn.ReturnType.Kind == IrTypeKind.Void ? null : ret,
                            modifiers: ["public", "override"],
                            body: m =>
                            {
                                m.Line($"// TODO: implement {fn.Name}");
                                if (fn.ReturnType.Kind != IrTypeKind.Void)
                                {
                                    if (fn.ReturnType.Kind == IrTypeKind.Struct)
                                        m.Line($"fatalError(\"{fn.Name} is not implemented\")");
                                    else
                                        m.Line($"return {DefaultSwiftValue(typeMap, fn.ReturnType, fn.Name)}");
                                }
                            });
                    }
                });
        }

        // =====================================================================
        // ---------- helpers ----------
        // =====================================================================

        private static void EmitSwiftInvocationHandler(ObjcEmitterContext ctx, SwiftWriter w)
        {
            var runtime = ctx.Runtime;
            var ext = ctx.ExtName;

            var name = $"{runtime.SwiftPrefix}{ext}_invocation_handler";
            // e.g. "__EXT_SWIFT__ExtDiscordSDK_invocation_handler"

            var ps = new[]
            {
                new SwiftParam(
                    External: "_",
                    Internal: runtime.RetBufferParam,           // "__ret_buffer"
                    Type: "UnsafeMutablePointer<CChar>?"),
                new SwiftParam(
                    External: "arg1",
                    Internal: runtime.RetBufferLengthParam,     // "__ret_buffer_length"
                    Type: "Double"),
            };

            w.Func(
                name: name,
                parameters: ps,
                returnType: "Double",
                modifiers: ["public"],
                body: body =>
                {
                    // var __bw = BufferWriter(base: UnsafeMutableRawPointer(__ret_buffer!), size: Int(__ret_buffer_length))
                    body.Line(
                        $"var {runtime.BufferWriterVar} = BufferWriter(" +
                        $"base: UnsafeMutableRawPointer({runtime.RetBufferParam}!), " +
                        $"size: Int({runtime.RetBufferLengthParam}))");

                    // return __dispatch_queue.fetch(into: &__bw)
                    body.Line($"return {runtime.DispatchQueueField}.fetch(into: &{runtime.BufferWriterVar})");
                });
        }

        private static void EmitSwiftQueueBuffer(ObjcEmitterContext ctx, SwiftWriter w)
        {
            var runtime = ctx.Runtime;
            var ext = ctx.ExtName;

            var name = $"{runtime.SwiftPrefix}{ext}_queue_buffer";
            // e.g. "__EXT_SWIFT__ExtDiscordSDK_queue_buffer"

            var ps = new[]
            {
                new SwiftParam(
                    External: "_",
                    Internal: runtime.ArgBufferParam,           // "__arg_buffer"
                    Type: "UnsafeMutablePointer<CChar>?"),
                new SwiftParam(
                    External: "arg1",
                    Internal: runtime.ArgBufferLengthParam,     // "__arg_buffer_length"
                    Type: "Double"),
            };

            w.Func(
                name: name,
                parameters: ps,
                returnType: "Double",
                modifiers: ["public"],
                body: body =>
                {
                    body.Lines($$"""
                        let size = Int({{runtime.ArgBufferLengthParam}})
                        guard size > 0, let base = UnsafeMutableRawPointer({{runtime.ArgBufferParam}}) else {
                            return 0.0
                        }

                        let buffer = GMBuffer(base: base, size: size)
                        __buffer_queue.append(buffer)
                        return 1.0
                        """);
                });
        }

        private void EmitInternalSwiftFunctions(ObjcEmitterContext ctx, ImmutableArray<IrFunction> fncs, SwiftWriter w, SwiftTypeMap typeMap)
        {
            var runtime = ctx.Runtime;
            var ext = ctx.ExtName;

            // ============================================================
            // 1) Open, user-overridable method with nice Swift types
            // ============================================================

            foreach (var fn in fncs)
            {
                var userParams = fn.Parameters.Select(p => new SwiftParam(External: p.Name, Internal: p.Name, Type: typeMap.Map(p.Type, owned: false)));

                string? userRetType =
                    fn.ReturnType.Kind == IrTypeKind.Void
                        ? null
                        : typeMap.Map(fn.ReturnType, owned: true);

                w.Func(
                    name: fn.Name,
                    parameters: userParams,
                    returnType: userRetType,
                    modifiers: ["open"],
                    body: body =>
                    {
                        body.Line($"// default stub for {fn.Name}");
                        if (fn.ReturnType.Kind != IrTypeKind.Void)
                        {
                            // fall back to something sane; you can reuse existing SwiftDefault if you like
                            if (fn.ReturnType.Kind == IrTypeKind.Struct)
                            {
                                body.Line($"fatalError(\"{fn.Name} is not implemented\")");
                            }
                            else
                            {
                                var defExpr = DefaultSwiftValue(typeMap, fn.ReturnType, fn.Name);
                                body.Line($"return {defExpr}");
                            }
                        }
                    });
                w.Line();
            }

            // ============================================================
            // 2) Wire entrypoint: __EXT_SWIFT__{fn.Name}
            //    This is what ObjC/ObjC++ calls.
            // ============================================================

            foreach (var fn in fncs)
            {
                var needsArgsBuffer = IrAnalysis.NeedsArgsBuffer(fn);
                var needsRetBuffer = IrAnalysis.NeedsRetBuffer(fn);

                var bridgeName = $"{runtime.SwiftPrefix}{fn.Name}";
                // If you don't have SwiftNativePrefix, hardcode "__EXT_SWIFT__".

                // Parameters for the wire entrypoint
                var ps = ExportTypeUtils.ParamsFor(fn, runtime);
                var r = ExportTypeUtils.ReturnFor(fn);

                w.Func(
                    name: bridgeName,
                    parameters: ps.AsSwift(),
                    returnType: r.AsSwiftType(),
                    modifiers: ["public"],
                    body: body =>
                    {
                        if (needsArgsBuffer || needsRetBuffer)
                        {
                            body.Line("do");
                            body.Block(doBlock =>
                            {
                                EmitInternalSwiftFunctionBody(fn, doBlock, runtime, needsArgsBuffer, needsRetBuffer);

                            }, true);
                            body.Line("catch");
                            body.Block(catchBlock =>
                            {
                                catchBlock.Line($"os_log(\"Corrupted buffer when calling '{fn.Name}'\", log: .default, type: .error)");
                                catchBlock.Line($"return {(r == ExportType.String ? "\"\"" : "-1")}");
                            }, true);
                        }
                        else
                        {
                            EmitInternalSwiftFunctionBody(fn, body, runtime, needsArgsBuffer, needsRetBuffer);
                        }
                    });
                w.Line();
            }
        }

        private void EmitInternalSwiftFunctionBody(IrFunction fn, SwiftWriter w, RuntimeNaming runtime, bool needsArgsBuffer, bool needsRetBuffer)
        {
            // 1) Decode arguments (if any)
            var callArgs = EmitDecode(
                w,
                fn,
                needsArgsBuffer: needsArgsBuffer,
                readerVar: runtime.BufferReaderVar);

            // 2) Call the open user-facing method
            var labeledArgs = string.Join(", ", callArgs);

            if (fn.ReturnType.Kind == IrTypeKind.Void)
            {
                w.Line($"self.{fn.Name}({labeledArgs})");
                w.Line("return 0.0");
                return;
            }

            // has return value
            w.Line($"let __result = self.{fn.Name}({labeledArgs})");

            // 3) Encode return via helper - either into buffer or as Double/String
            EmitEncodeReturn(
                w,
                fn.ReturnType,
                resultExpr: "__result",
                needsRetBuffer: needsRetBuffer,
                writerVar: runtime.BufferWriterVar);
        }

        /// <summary>
        /// Decode arguments for a function into locals and build labeled call arguments
        /// for the user-facing method.
        ///
        /// - If needsArgsBuffer == true: read from BufferReader over (__arg_buffer, __arg_buffer_length)
        /// - If needsArgsBuffer == false: parameters are passed directly (as Doubles / Strings),
        ///   and we just convert them into the Swift types the user method expects.
        /// </summary>
        public List<string> EmitDecode(SwiftWriter w, IrFunction fn, bool needsArgsBuffer, string readerVar)
        {
            SwiftWireHelpers wireHelpers = new(runtime, typeMap);

            var callArgs = new List<string>();

            if (needsArgsBuffer)
            {
                // var __br = BufferReader(base: UnsafeRawPointer(__arg_buffer!), size: Int(__arg_buffer_length))
                w.Line(
                    $"var {readerVar} = BufferReader(" +
                    $"base: UnsafeRawPointer({runtime.ArgBufferParam}!), " +
                    $"size: Int({runtime.ArgBufferLengthParam}))");
                w.Line();

                foreach (var p in fn.Parameters)
                {
                    w.Line($"// field: {p.Name}, type: {p.Type.Name}{(p.Type.IsCollection ? $"[{p.Type.FixedLength}]" : "")}");
                    wireHelpers.DecodeLines(
                        w,
                        p.Type,
                        accessor: p.Name,
                        declare: true,
                        bufferVar: readerVar, owned: false);
                    w.Line();

                    // user method uses label == name
                    callArgs.Add($"{p.Name}: {p.Name}");
                }

                return callArgs;
            }

            // -------- Direct-arg mode (no arg buffer) ----------
            // Here each parameter appears as its own Swift parameter
            // on the __EXT_SWIFT__ method (e.g. Double, String).
            //
            // We convert from that "bridge" representation to
            // the actual Swift type the user-friendly method expects.

            foreach (var p in fn.Parameters)
            {
                var bridgeName = p.Name; // same name for the parameter in __EXT_SWIFT__ signature
                var t = p.Type;
                string expr;

                if (t.IsNumericScalar)
                {
                    var swiftType = typeMap.Map(t, owned: true);

                    if (t.Name == "bool" || t.Name == "Bool")
                    {
                        expr = $"{bridgeName} != 0";
                    }
                    else
                    {
                        expr = $"{swiftType}({bridgeName})";
                    }
                }
                else if (t.IsStringScalar)
                {
                    // Bridge type is String, and user type is String too.
                    expr = bridgeName;
                }
                else
                {
                    // If you ever mark more cases as "direct", handle them here.
                    // For now, just pass directly (you can refine later).
                    expr = bridgeName;
                }

                callArgs.Add($"{p.Name}: {expr}");
            }

            return callArgs;
        }

        /// <summary>
        /// Emit Swift code to encode the return value of a function, either:
        /// - directly as Double or String when no ret buffer is needed, or
        /// - into a BufferWriter over (__ret_buffer, __ret_buffer_length) when needed.
        /// </summary>
        public void EmitEncodeReturn(SwiftWriter w, IrType ret, string resultExpr, bool needsRetBuffer, string writerVar)
        {
            // Void return – always just 0.0
            if (ret.Kind == IrTypeKind.Void)
            {
                w.Line("return 0.0");
                return;
            }

            if (needsRetBuffer)
            {
                SwiftWireHelpers wireHelpers = new(runtime, typeMap);

                // Use BufferWriter over (__ret_buffer, __ret_buffer_length)
                w.Lines(
                    $"var {writerVar} = BufferWriter(" +
                    $"base: UnsafeMutableRawPointer({runtime.RetBufferParam}!), " +
                    $"size: Int({runtime.RetBufferLengthParam}))");
                w.Line();
                w.Line($"// return: {resultExpr}, type: {ret.Name}{(ret.IsCollection ? $"[{ret.FixedLength}]" : "")}");
                wireHelpers.EncodeLines(w, ret, accessor: resultExpr, bufferVar: writerVar);
                w.Line("return 0.0");
                return;
            }

            // Direct-return path: map into Swift type that bridge expects.

            if (ret.IsNumericScalar)
            {
                if (ret.Name == "bool")
                    w.Line($"return {resultExpr} ? 1.0 : 0.0");
                else
                    w.Line($"return Double({resultExpr})");
                return;
            }

            if (ret.IsStringScalar)
            {
                // Direct string return: Swift String -> C++ std::string via CxxStdlib bridge.
                w.Line($"return {resultExpr}");
                return;
            }

            // Fallback – shouldn’t really happen with current IR rules.
            w.Line("return 0.0");
        }

        /// <summary>
        /// Simple default value generator for user stubs
        /// (you can reuse / merge with your existing SwiftDefault).
        /// </summary>
        private static string DefaultSwiftValue(SwiftTypeMap typeMap, IrType t, string funcName)
        {
            if (t.Kind == IrTypeKind.Void) return "";

            if (t.IsCollection)
                return $"[]";

            if (t.IsNullable)
                return "nil";

            if (t.Kind == IrTypeKind.Struct)
                return $"{typeMap.Map(t, owned: true)}()";

            if (t.Kind == IrTypeKind.Enum)
                return $"{typeMap.Map(t, owned: true)}(rawValue: 0)!";

            if (t.Kind == IrTypeKind.Scalar && t.Name == "string")
                return "\"\"";

            if (t.Kind == IrTypeKind.Scalar && t.Name == "bool")
                return "false";

            if (t.IsNumericScalar)
                return "0";

            return "0";
        }
    }
}