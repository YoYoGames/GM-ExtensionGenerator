using codegencore.Writers.Lang;
using extgen.Bridge;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem.Swift;

namespace extgen.Emitters.Swift
{
    internal sealed class SwiftWireHelpers(RuntimeNaming runtime, SwiftTypeMap typeMap) : WireHelpersBase<SwiftWriter>
    {
        // ----------------------------------------------------
        // 1) Low-level read / write expressions (non-collection)
        // ----------------------------------------------------

        private string ReadEnumExpr(IrType t, string readerVar, out string swiftEnumType)
        {
            // Swift enum type name (without namespace, per your SwiftTypeMap)
            swiftEnumType = typeMap.MapEnum(t);

            // Underlying scalar type – e.g. Int32, UInt8...
            var underlying = t.Underlying ?? throw new InvalidOperationException(
                $"Enum {t.Name} has no underlying scalar type.");
            var rawType = typeMap.MapScalar(underlying, owned: false);

            // Read raw scalar, then wrap into enum with rawValue:
            //   let raw = try r.readRaw(Int32.self)
            //   return MyEnum(rawValue: raw)!
            // Here we just return the expression using a local name.
            var rawLocal = $"__raw_{t.Name}";
            // This helper is used in DecodeLines; it will declare the local if needed.
            return $"({swiftEnumType}(rawValue: try {readerVar}.readRaw({rawType}.self))!)";
        }

        private string ReadExprInternal(IrType t, string readerVar)
        {
            if (t.IsNullable || t.IsCollection)
                throw new NotSupportedException("ReadExprInternal expects a non-nullable, non-collection type.");

            // Enums: read underlying + wrap
            if (t.Kind == IrTypeKind.Enum && t.Underlying is not null)
                return ReadEnumExpr(t, readerVar, out _);

            // Any: raw GMValue
            if (t.Kind == IrTypeKind.Any)
                return $"try {readerVar}.readGMValue()";

            // AnyArray / AnyMap are handled in DecodeLines with multiple statements.
            if (t.Kind == IrTypeKind.AnyArray || t.Kind == IrTypeKind.AnyMap)
                throw new NotSupportedException("ReadExprInternal does not handle AnyArray/AnyMap; use DecodeLines.");

            // Buffer / Function
            if (t.Kind == IrTypeKind.Buffer)
                return $"{runtime.BufferQueueField}.removeFirst()";

            if (t.Kind == IrTypeKind.Function)
                return $"try {readerVar}.readGMFunction({runtime.DispatchQueueField})";

            // Plain scalar / struct / ITypedStruct / etc
            var swiftType = typeMap.Map(t, owned: true);
            return $"try {readerVar}.readRaw({swiftType}.self)";
        }

        private string WriteExprInternal(IrType t, string writerVar, string valueExpr)
        {
            // Non-nullable, non-collection.
            if (t.IsNullable || t.IsCollection)
                throw new NotSupportedException("WriteExprInternal expects a non-nullable, non-collection type.");

            // Enums: encode their rawValue (scalar)
            if (t.Kind == IrTypeKind.Enum && t.Underlying is not null)
            {
                return $"try {writerVar}.writeRaw({valueExpr}.rawValue)";
            }

            // Any / AnyArray / AnyMap -> GMValue world
            if (t.Kind == IrTypeKind.Any ||
                t.Kind == IrTypeKind.AnyArray ||
                t.Kind == IrTypeKind.AnyMap)
            {
                return $"try {writerVar}.writeGMValue({valueExpr})";
            }

            if (t.Kind == IrTypeKind.Buffer)
                throw new NotSupportedException("Swift wire: Buffer fields not yet supported in struct codecs.");
            if (t.Kind == IrTypeKind.Function)
                throw new NotSupportedException("Swift wire: Function fields not yet supported in struct codecs.");

            // Everything else: raw codec
            return $"try {writerVar}.writeRaw({valueExpr})";
        }

        // ----------------------------------------------------
        // 2) High-level Decode / Encode lines
        // ----------------------------------------------------

        public override string ReadExpr(IrType t, string bufferVar)
        {
            return ReadExprInternal(t, bufferVar);
        }

        public override string WriteExpr(IrType t, string bufferVar, string valueExpr)
        {
            return WriteExprInternal(t, bufferVar, valueExpr);
        }

        public override void DecodeLines(SwiftWriter w, IrType t, string accessor, bool declare, string bufferVar) =>
            DecodeLines(w, t, accessor, declare, bufferVar, owned: true);

        public override void EncodeLines(SwiftWriter w, IrType t, string accessor, string bufferVar)
        {
            // Nullable: writeRaw handles Optional<T> layout (presence + payload).
            if (t.IsNullable)
            {
                w.Line($"try {bufferVar}.writeRaw({accessor})");
                return;
            }

            // Collections
            if (t.IsCollection)
            {
                if (t.FixedLength is int)
                {
                    // No length prefix; just contiguous elements
                    w.Line($"try {bufferVar}.writeRawFixedArray({accessor})");
                }
                else
                {
                    // Int32 length + elements
                    w.Line($"try {bufferVar}.writeRawList({accessor})");
                }

                return;
            }

            // Non-nullable, non-collection: defer to scalar helper
            var stmt = WriteExprInternal(t, bufferVar, accessor);
            w.Line(stmt);
        }

        // ============================================================
        //  High-level DecodeLines with ownership control
        // ============================================================

        public void DecodeLines(SwiftWriter w, IrType t, string accessor, bool declare, string bufferVar, bool owned)
        {
            // Swift type name to use in a let/var if declare=true
            string? swiftTypeForDecl = declare ? typeMap.Map(t, owned: owned) : null;

            void EmitAssign(string rhs)
            {
                if (declare)
                    w.Let(accessor, swiftTypeForDecl, rhs);
                else
                    w.Assign(accessor, rhs);
            }

            // Nullable → Optional<T>
            if (t.IsNullable)
            {
                var inner = t with { IsNullable = false };

                // For Any-like types we can't use readRawOptional<T>, because T isn't supported by readRaw.
                if (inner.Kind == IrTypeKind.Any)
                {
                    // layout: Bool hasValue + GMValue if present
                    w.If($"try {bufferVar}.readRaw(Bool.self)", thenBody =>
                    {
                        thenBody.Assign(accessor, $"try {bufferVar}.readGMValue()");
                    }, elseBody =>
                    {
                        elseBody.Assign(accessor, "nil");
                    });
                    return;
                }

                // Everything else: use readRawOptional<T>()
                var innerSwiftType = typeMap.Map(inner, owned: owned);
                var expr = $"try {bufferVar}.readRawOptional({innerSwiftType}.self)";
                EmitAssign(expr);
                return;
            }

            // Collections
            if (t.IsCollection)
            {
                var el = IrHelpers.Element(t);
                var elSwift = typeMap.Map(el, owned: true);

                // Fixed-length: no length prefix on wire
                if (t.FixedLength is int n)
                {
                    var expr = $"try {bufferVar}.readRawVector({elSwift}.self, count: {n})";
                    EmitAssign(expr);
                }
                else
                {
                    // Variable-length: Int32 length + elements
                    var expr = $"try {bufferVar}.readRaw([{elSwift}].self)";
                    EmitAssign(expr);
                }

                return;
            }

            // Non-nullable, non-collection

            if (t.Kind == IrTypeKind.Any)
            {
                var expr = $"try {bufferVar}.readGMValue()";
                EmitAssign(expr);
                return;
            }

            if (t.Kind == IrTypeKind.AnyArray)
            {
                w.Guard($"case .array(let arr) = try {bufferVar}.readGMValue()", guardBody =>
                {
                    guardBody.Line("throw GMError.typeMismatch(\"expected GMValue.array\")");
                });

                EmitAssign("arr");
                return;
            }

            if (t.Kind == IrTypeKind.AnyMap)
            {
                // guard case .object(let obj) = try r.readGMValue() else { throw ... }
                w.Guard($"case .object(let obj) = try {bufferVar}.readGMValue()", guardBody =>
                {
                    guardBody.Line("throw GMError.typeMismatch(\"expected GMValue.object\")");
                });

                var mapExpr = "obj.map { ($0.key, $0.value) }";
                EmitAssign(mapExpr);
                return;
            }

            // Enums / scalars / structs / etc
            if (t.Kind == IrTypeKind.Enum && t.Underlying is not null)
            {
                var expr = ReadExprInternal(t, bufferVar);
                EmitAssign(expr);
                return;
            }

            // Default: readRaw<T>()
            EmitAssign(ReadExprInternal(t, bufferVar));
        }
    }
}
