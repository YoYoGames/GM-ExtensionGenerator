using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge.Swift;
using extgen.Emitters.AppleMobile;
using extgen.Emitters.AppleMobile.Objc;
using extgen.Emitters.Utils;
using extgen.Extensions;
using extgen.Models;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.TypeSystem.Cpp;
using extgen.TypeSystem.Swift;
using extgen.Utils;
using System.Collections.Immutable;

namespace extgen.Emitters.AppleMobile.Swift
{
    internal sealed record AppleEmitServices(
        IIrTypeEnumResolver Enums
    );

    public sealed class SwiftEmitter(IAppleMobileEmitterSettings settings, RuntimeNaming runtime) : IIrEmitter
    {
        private readonly SwiftTypeMap typeMap = new();

        public void Emit(IrCompilation comp, string dir)
        {
            ObjcEmitterContext ctx = new(comp.Name, settings, runtime);
            ObjcLayout layout = new(dir, settings);

            EmitAll(ctx, comp, layout);
        }

        private void EmitAll(ObjcEmitterContext ctx, IrCompilation c, ObjcLayout layout)
        {
            CppTypeMap cppTypeMap = new(ctx.Runtime);

            // Swift flavor bridge on ObjC side
            SwiftBridge bridge = new();
            var enums = new IrTypeEnumResolver(c.Enums);

            ObjcCommonEmitter common = new(ctx, cppTypeMap, bridge);
            common.EmitInternal(c, layout);
            common.EmitObjcUserShell(c, layout);

            // Swift base (always regenerated)
            FileEmitHelpers.WriteSwift(layout.CodeGenDir, $"{ctx.ExtName}Artifacts.swift", w => EmitArtifacts(w, c, enums));
            FileEmitHelpers.WriteSwift(layout.CodeGenDir, $"{ctx.ExtName}InternalSwift.swift", w => EmitInternalSwift(ctx, c, w, enums));

            // user Swift file (only if missing)
            FileEmitHelpers.WriteSwiftIfMissing(layout.SourceDir, $"{ctx.ExtName}Swift.swift", w => EmitUserSwift(ctx, c, w));
        }

        // =====================================================================
        // 1. SWIFT ARTIFACTS
        // =====================================================================

        private void EmitArtifacts(SwiftWriter w, IrCompilation c, IIrTypeEnumResolver enums)
        {
            EmitConsts(w, c.Constants);
            EmitEnums(w, c.Enums);
            EmitStructs(w, c.Structs);
            EmitStructCodecs(w, c.Structs, enums);
        }

        // ------------------- Constants -------------------

        private void EmitConsts(SwiftWriter w, ImmutableArray<IrConstant> constants)
        {
            foreach (var c in constants)
            {
                w.Let(c.Name, typeMap.Map(c.Type), c.Literal);
            }
        }

        // --------------------- Enums ---------------------

        private void EmitEnums(SwiftWriter w, IImmutableList<IrEnum> enums)
        {
            foreach (var e in enums)
            {
                var underlying = e.Underlying
                    ?? throw new InvalidOperationException($"Enum {e.Name} has no underlying type.");

                var rawType = typeMap.Map(underlying, owned: false);

                var members = e.Members.Select(m => new EnumMember(
                    m.Name,
                    m.DefaultLiteral,
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

        // --------------------- Struct codecs ---------------------

        private void EmitStructCodecs(SwiftWriter w, IImmutableList<IrStruct> structs, IIrTypeEnumResolver enums)
        {
            int codecId = 0;
            SwiftWireHelpers wireHelpers = new(runtime, typeMap, enums);

            foreach (var s in structs)
            {
                w.Extension(s.Name, body =>
                {
                    body.Line($"public static let codecID: UInt32 = {codecId}");
                    body.Line();

                    body.Line("public init<R: IByteReader>(_ r: inout R) throws");
                    body.Block(init =>
                    {
                        foreach (var f in s.Fields)
                        {
                            wireHelpers.DecodeLines(init, f.Type, $"self.{f.Name}", declare: false, bufferVar: "r");
                        }
                    }, trailingNewLine: true);

                    body.Line();

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
        // 2. INTERNAL SWIFT
        // =====================================================================

        private void EmitInternalSwift(ObjcEmitterContext ctx, IrCompilation c, SwiftWriter w, IIrTypeEnumResolver enums)
        {
            var ext = c.Name;
            var rt = ctx.Runtime;

            // Imports
            w.Import("Foundation")
             .Import("os.log")
             .Import("CxxStdlib")
             .Line();

            var usesFunctions = c.HasFunctionType();
            var usesBuffers = c.HasBufferType();

            w.Class(
                name: $"{ext}InternalSwift",
                modifiers: ["open"],
                inher: null,
                body: cls =>
                {
                    if (usesFunctions)
                    {
                        cls.Var(
                            name: rt.DispatchQueueField,
                            type: "GMDispatchQueue",
                            init: "GMDispatchQueue()",
                            modifiers: ["internal"]);
                    }

                    if (usesBuffers)
                    {
                        cls.Var(
                            name: rt.BufferQueueField,
                            type: "[GMBuffer]",
                            init: "[]",
                            modifiers: ["internal"]);
                    }

                    cls.Line();

                    cls.Init(parameters: [], modifiers: ["public"], body: _ => { });
                    cls.Line();

                    EmitInternalSwiftFunctions(ctx, c, cls, typeMap, enums);

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

        // =====================================================================
        // 3. USER SWIFT STUB
        // =====================================================================

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
                        var ret = typeMap.Map(fn.ReturnType, owned: false);

                        var ps = fn.Parameters.Select(p =>
                            new SwiftParam(
                                External: p.Name,
                                Internal: p.Name,
                                Type: typeMap.Map(p.Type, owned: false))
                        );

                        cls.Func(
                            fn.Name,
                            ps,
                            IrTypeUtil.IsVoid(fn.ReturnType) ? null : ret,
                            modifiers: ["public", "override"],
                            body: m =>
                            {
                                m.Line($"// TODO: implement {fn.Name}");

                                if (!IrTypeUtil.IsVoid(fn.ReturnType))
                                {
                                    if (fn.ReturnType is IrType.Named { Kind: NamedKind.Struct })
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
            var rt = ctx.Runtime;
            var ext = ctx.ExtName;

            var name = $"{rt.SwiftPrefix}{ext}_invocation_handler";

            var ps = new[]
            {
                new SwiftParam("_", rt.RetBufferParam, "UnsafeMutablePointer<CChar>?"),
                new SwiftParam("arg1", rt.RetBufferLengthParam, "Double"),
            };

            w.Func(
                name: name,
                parameters: ps,
                returnType: "Double",
                modifiers: ["public"],
                body: body =>
                {
                    body.Line(
                        $"var {rt.BufferWriterVar} = BufferWriter(" +
                        $"base: UnsafeMutableRawPointer({rt.RetBufferParam}!), " +
                        $"size: Int({rt.RetBufferLengthParam}))");

                    body.Line($"return {rt.DispatchQueueField}.fetch(into: &{rt.BufferWriterVar})");
                });
        }

        private static void EmitSwiftQueueBuffer(ObjcEmitterContext ctx, SwiftWriter w)
        {
            var rt = ctx.Runtime;
            var ext = ctx.ExtName;

            var name = $"{rt.SwiftPrefix}{ext}_queue_buffer";

            var ps = new[]
            {
                new SwiftParam("_", rt.ArgBufferParam, "UnsafeMutablePointer<CChar>?"),
                new SwiftParam("arg1", rt.ArgBufferLengthParam, "Double"),
            };

            w.Func(
                name: name,
                parameters: ps,
                returnType: "Double",
                modifiers: ["public"],
                body: body =>
                {
                    body.Lines($$"""
                        let size = Int({{rt.ArgBufferLengthParam}})
                        guard size > 0, let base = UnsafeMutableRawPointer({{rt.ArgBufferParam}}) else {
                            return 0.0
                        }

                        let buffer = GMBuffer(base: base, size: size)
                        __buffer_queue.append(buffer)
                        return 1.0
                        """);
                });
        }

        private void EmitInternalSwiftFunctions(ObjcEmitterContext ctx, IrCompilation c, SwiftWriter w, SwiftTypeMap typeMap, IIrTypeEnumResolver enums)
        {
            var rt = ctx.Runtime;

            // 1) Open user-overridable methods
            var allFunctions = c.GetAllFunctions(IrFunctionUtil.PatchStructMethod);
            foreach (var fn in allFunctions)
            {
                var userParams = fn.Parameters.Select(p =>
                    new SwiftParam(p.Name, p.Name, typeMap.Map(p.Type, owned: false)));

                string? userRetType =
                    IrTypeUtil.IsVoid(fn.ReturnType) ? null : typeMap.Map(fn.ReturnType, owned: true);

                w.Func(
                    name: fn.Name,
                    parameters: userParams,
                    returnType: userRetType,
                    modifiers: ["open"],
                    body: body =>
                    {
                        body.Line($"// default stub for {fn.Name}");

                        if (!IrTypeUtil.IsVoid(fn.ReturnType))
                        {
                            if (fn.ReturnType is IrType.Named { Kind: NamedKind.Struct })
                                body.Line($"fatalError(\"{fn.Name} is not implemented\")");
                            else
                                body.Line($"return {DefaultSwiftValue(typeMap, fn.ReturnType, fn.Name)}");
                        }
                    });

                w.Line();
            }

            // 2) Wire entrypoints: __EXT_SWIFT__{fn.Name}
            foreach (var fn in allFunctions)
            {
                var needsArgsBuffer = IrAnalysis.NeedsArgsBuffer(fn);
                var needsRetBuffer = IrAnalysis.NeedsRetBuffer(fn);

                var bridgeName = $"{rt.SwiftPrefix}{fn.Name}";
                var ps = ExportTypeUtils.ParamsFor(fn, rt);
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
                                EmitInternalSwiftFunctionBody(fn, doBlock, rt, needsArgsBuffer, needsRetBuffer, enums);
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
                            EmitInternalSwiftFunctionBody(fn, body, rt, needsArgsBuffer, needsRetBuffer, enums);
                        }
                    });

                w.Line();
            }
        }

        private void EmitInternalSwiftFunctionBody(IrFunction fn, SwiftWriter w, RuntimeNaming rt, bool needsArgsBuffer, bool needsRetBuffer, IIrTypeEnumResolver enums)
        {
            var callArgs = EmitDecode(w, fn, needsArgsBuffer: needsArgsBuffer, readerVar: rt.BufferReaderVar, enums);
            var labeledArgs = string.Join(", ", callArgs);

            if (IrTypeUtil.IsVoid(fn.ReturnType))
            {
                w.Line($"self.{fn.Name}({labeledArgs})");
                w.Line("return 0.0");
                return;
            }

            w.Line($"let __result = self.{fn.Name}({labeledArgs})");

            EmitEncodeReturn(
                w,
                fn.ReturnType,
                resultExpr: "__result",
                needsRetBuffer: needsRetBuffer,
                writerVar: rt.BufferWriterVar, enums);
        }

        public List<string> EmitDecode(SwiftWriter w, IrFunction fn, bool needsArgsBuffer, string readerVar, IIrTypeEnumResolver enums)
        {
            SwiftWireHelpers wireHelpers = new(runtime, typeMap, enums);

            var callArgs = new List<string>();

            if (needsArgsBuffer)
            {
                w.Line(
                    $"var {readerVar} = BufferReader(" +
                    $"base: UnsafeRawPointer({runtime.ArgBufferParam}!), " +
                    $"size: Int({runtime.ArgBufferLengthParam}))");
                w.Line();

                foreach (var p in fn.Parameters)
                {
                    w.Line($"// field: {p.Name}, type: {IrTypeUtil.ToDebugString(p.Type)}");

                    wireHelpers.DecodeLines(
                        w,
                        p.Type,
                        accessor: p.Name,
                        declare: true,
                        bufferVar: readerVar,
                        owned: false);

                    w.Line();
                    callArgs.Add($"{p.Name}: {p.Name}");
                }

                return callArgs;
            }

            // Direct-arg mode: convert from bridge primitives -> user-facing Swift type
            foreach (var p in fn.Parameters)
            {
                var bridgeName = p.Name;
                var t = p.Type;

                string expr;

                if (IrTypeUtil.IsNumericScalar(t))
                {
                    var swiftType = typeMap.Map(t, owned: true);

                    if (IrTypeUtil.IsBool(t))
                        expr = $"{bridgeName} != 0";
                    else
                        expr = $"{swiftType}({bridgeName})";
                }
                else if (IrTypeUtil.IsStringScalar(t))
                {
                    expr = bridgeName;
                }
                else
                {
                    expr = bridgeName;
                }

                callArgs.Add($"{p.Name}: {expr}");
            }

            return callArgs;
        }

        public void EmitEncodeReturn(SwiftWriter w, IrType ret, string resultExpr, bool needsRetBuffer, string writerVar, IIrTypeEnumResolver enums)
        {
            if (IrTypeUtil.IsVoid(ret))
            {
                w.Line("return 0.0");
                return;
            }

            if (needsRetBuffer)
            {
                SwiftWireHelpers wireHelpers = new(runtime, typeMap, enums);

                w.Lines(
                    $"var {writerVar} = BufferWriter(" +
                    $"base: UnsafeMutableRawPointer({runtime.RetBufferParam}!), " +
                    $"size: Int({runtime.RetBufferLengthParam}))");
                w.Line();

                w.Line($"// return: {resultExpr}, type: {IrTypeUtil.ToDebugString(ret)}");
                wireHelpers.EncodeLines(w, ret, accessor: resultExpr, bufferVar: writerVar);

                w.Line("return 0.0");
                return;
            }

            // Direct-return path: ONLY numeric scalar or string scalar
            // Nullable is not representable as Double/String -> fallback
            if (IrTypeUtil.ContainsNullable(ret))
            {
                w.Line("return 0.0");
                return;
            }

            if (IrTypeUtil.IsNumericScalar(ret))
            {
                if (IrTypeUtil.IsBool(ret))
                    w.Line($"return {resultExpr} ? 1.0 : 0.0");
                else
                    w.Line($"return Double({resultExpr})");

                return;
            }

            if (IrTypeUtil.IsStringScalar(ret))
            {
                w.Line($"return {resultExpr}");
                return;
            }

            w.Line("return 0.0");
        }

        private static string DefaultSwiftValue(SwiftTypeMap typeMap, IrType t, string funcName)
        {
            _ = funcName;

            // Arrays -> []
            if (t is IrType.Array)
                return "[]";

            // Nullable -> nil
            if (t is IrType.Nullable)
                return "nil";

            // Named struct -> Type()
            if (t is IrType.Named { Kind: NamedKind.Struct } ns)
                return $"{typeMap.Map(ns, owned: true)}()";

            // Named enum -> rawValue: 0
            if (t is IrType.Named { Kind: NamedKind.Enum } ne)
                return $"{typeMap.Map(ne, owned: true)}(rawValue: 0)!";

            // Builtins
            if (IrTypeUtil.IsStringScalar(t)) return "\"\"";
            if (IrTypeUtil.IsBool(t)) return "false";
            if (IrTypeUtil.IsNumericScalar(t)) return "0";

            return "0";
        }
    }
}
