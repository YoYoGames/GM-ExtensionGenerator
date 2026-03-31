using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.TypeSystem.Swift;

namespace extgen.Emitters.AppleMobile.Swift
{
    /// <summary>
    /// Provides Swift-specific wire protocol helpers for encoding and decoding types.
    /// Handles atomic reads/writes, nullable types, arrays, enums, and special types like Any, Buffer, and Function.
    /// </summary>
    internal sealed class SwiftWireHelpers : WireHelpersBase<SwiftWriter>
    {
        private readonly RuntimeNaming _runtime;
        private readonly SwiftTypeMap _typeMap;
        private readonly IIrTypeEnumResolver _enums;

        public SwiftWireHelpers(RuntimeNaming runtime, SwiftTypeMap typeMap, IIrTypeEnumResolver enums)
        {
            _runtime = runtime;
            _typeMap = typeMap;
            _enums = enums;
        }

        // Low-level read / write expressions (atomic only)

        private IrType GetEnumUnderlyingOrThrow(string enumName)
        {
            if (!_enums.TryGetUnderlying(enumName, out var underlying))
                throw new NotSupportedException($"Enum underlying type not found for '{enumName}'.");
            return IrType.StripNullable(underlying);
        }

        private string ReadEnumExpr(string enumName, string readerVar)
        {
            // Swift enum type name (typically just the name from SwiftTypeMap)
            var swiftEnumType = enumName;

            // Underlying scalar type - e.g. Int32, UInt8
            var underlying = GetEnumUnderlyingOrThrow(enumName);

            // Enum underlying is expected to be a builtin scalar (int/uint/bool).
            // If string enums are allowed, BuiltinKind.String must be handled here as well.
            var rawSwiftType = _typeMap.Map(underlying, owned: true);

            // Read raw scalar, then wrap into enum with rawValue:
            //   MyEnum(rawValue: try r.readRaw(Int32.self))!
            return $"({swiftEnumType}(rawValue: try {readerVar}.readRaw({rawSwiftType}.self))!)";
        }

        private string ReadExprInternal(IrType t, string readerVar)
        {
            // Atomic read only: DecodeLines handles Nullable/Array and AnyArray/AnyMap guards.
            if (IrType.IsNullable(t) || t is IrType.Array)
                throw new NotSupportedException("ReadExprInternal expects a non-nullable, non-collection type.");

            return t switch
            {
                // Enums: read underlying + wrap
                IrType.Named { Kind: NamedKind.Enum, Name: var en } => ReadEnumExpr(en, readerVar),

                // Any: GMValue expression is OK (single statement)
                IrType.Builtin { Kind: BuiltinKind.Any } => $"try {readerVar}.readGMValue()",

                // AnyArray/AnyMap: require guard extraction (multi-line)
                IrType.Builtin { Kind: BuiltinKind.AnyArray } =>
                    throw new NotSupportedException("ReadExprInternal does not handle AnyArray; use DecodeLines."),
                IrType.Builtin { Kind: BuiltinKind.AnyMap } =>
                    throw new NotSupportedException("ReadExprInternal does not handle AnyMap; use DecodeLines."),

                // Buffer / Function
                IrType.Builtin { Kind: BuiltinKind.Buffer } => $"{_runtime.BufferQueueField}.removeFirst()",
                IrType.Builtin { Kind: BuiltinKind.Function } => $"try {readerVar}.readGMFunction({_runtime.DispatchQueueField})",

                // Structs + builtins scalars: readRaw<T>()
                _ =>
                    $"try {readerVar}.readRaw({_typeMap.Map(t, owned: true)}.self)"
            };
        }

        private string WriteExprInternal(IrType t, string writerVar, string valueExpr)
        {
            // Atomic write only: EncodeLines handles Nullable/Array.
            if (IrType.IsNullable(t) || t is IrType.Array)
                throw new NotSupportedException("WriteExprInternal expects a non-nullable, non-collection type.");

            return t switch
            {
                // Enums: encode rawValue
                IrType.Named { Kind: NamedKind.Enum } =>
                    $"try {writerVar}.writeRaw({valueExpr}.rawValue)",

                // Any / AnyArray / AnyMap -> GMValue world
                IrType.Builtin { Kind: BuiltinKind.Any } =>
                    $"try {writerVar}.writeGMValue({valueExpr})",
                IrType.Builtin { Kind: BuiltinKind.AnyArray } =>
                    $"try {writerVar}.writeGMValue({valueExpr})",
                IrType.Builtin { Kind: BuiltinKind.AnyMap } =>
                    $"try {writerVar}.writeGMValue({valueExpr})",

                IrType.Builtin { Kind: BuiltinKind.Buffer } =>
                    throw new NotSupportedException("Swift wire: Buffer fields not yet supported in struct codecs."),
                IrType.Builtin { Kind: BuiltinKind.Function } =>
                    throw new NotSupportedException("Swift wire: Function fields not yet supported in struct codecs."),

                // Everything else: raw codec
                _ => $"try {writerVar}.writeRaw({valueExpr})"
            };
        }

        // WireHelpersBase API

        public override string ReadExpr(IrType t, string bufferVar) => ReadExprInternal(t, bufferVar);

        public override string WriteExpr(IrType t, string bufferVar, string valueExpr) =>
            WriteExprInternal(t, bufferVar, valueExpr);

        public override void DecodeLines(SwiftWriter w, IrType t, string accessor, bool declare, string bufferVar) =>
            DecodeLines(w, t, accessor, declare, bufferVar, owned: true);

        public override void EncodeLines(SwiftWriter w, IrType t, string accessor, string bufferVar)
        {
            // Nullable: writeRaw handles Optional<T> layout (presence + payload).
            if (IrType.IsNullable(t))
            {
                w.Line($"try {bufferVar}.writeRaw({accessor})");
                return;
            }

            t = IrType.StripNullable(t);

            // Arrays
            if (t is IrType.Array a)
            {
                if (a.FixedLength is int)
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

            // Atomic
            w.Line(WriteExprInternal(t, bufferVar, accessor));
        }

        // High-level DecodeLines (supports Nullable/Array/AnyArray/AnyMap)

        /// <summary>
        /// Emits multi-line decoding logic for complex types including nullable types, arrays, and dynamic types.
        /// </summary>
        public void DecodeLines(SwiftWriter w, IrType t, string accessor, bool declare, string bufferVar, bool owned)
        {
            string? swiftTypeForDecl = declare ? _typeMap.Map(t, owned: owned) : null;

            void EmitAssign(string rhs)
            {
                if (declare)
                    w.Let(accessor, swiftTypeForDecl, rhs);
                else
                    w.Assign(accessor, rhs);
            }

            // Nullable -> Optional<T>
            if (IrType.IsNullable(t))
            {
                var inner = IrType.StripNullable(t);

                if (inner is IrType.Builtin { Kind: BuiltinKind.Any })
                {
                    w.If($"try {bufferVar}.readRaw(Bool.self)", thenBody =>
                    {
                        thenBody.Assign(accessor, $"try {bufferVar}.readGMValue()");
                    }, elseBody =>
                    {
                        elseBody.Assign(accessor, "nil");
                    });
                    return;
                }

                // Default: use readRawOptional<T>
                var innerSwiftType = _typeMap.Map(inner, owned: owned);
                EmitAssign($"try {bufferVar}.readRawOptional({innerSwiftType}.self)");
                return;
            }

            t = IrType.StripNullable(t);

            // Arrays
            if (t is IrType.Array a)
            {
                var el = a.Element;
                var elSwift = _typeMap.Map(el, owned: true);

                if (a.FixedLength is int n)
                {
                    // Fixed-length: no length prefix on wire
                    EmitAssign($"try {bufferVar}.readRawVector({elSwift}.self, count: {n})");
                }
                else
                {
                    // Variable-length: Int32 length + elements
                    EmitAssign($"try {bufferVar}.readRaw([{elSwift}].self)");
                }

                return;
            }

            // Non-nullable, non-collection

            // Any: GMValue
            if (t is IrType.Builtin { Kind: BuiltinKind.Any })
            {
                EmitAssign($"try {bufferVar}.readGMValue()");
                return;
            }

            // AnyArray: read GMValue, require .array
            if (t is IrType.Builtin { Kind: BuiltinKind.AnyArray })
            {
                w.Guard($"case .array(let arr) = try {bufferVar}.readGMValue()", guardBody =>
                {
                    guardBody.Line("throw GMError.typeMismatch(\"expected GMValue.array\")");
                });

                EmitAssign("arr");
                return;
            }

            // AnyMap: read GMValue, require .object, then convert to [(String, GMValue)]
            if (t is IrType.Builtin { Kind: BuiltinKind.AnyMap })
            {
                w.Guard($"case .object(let obj) = try {bufferVar}.readGMValue()", guardBody =>
                {
                    guardBody.Line("throw GMError.typeMismatch(\"expected GMValue.object\")");
                });

                EmitAssign("obj.map { ($0.key, $0.value) }");
                return;
            }

            // Enum: underlying + wrap (expression)
            if (t is IrType.Named { Kind: NamedKind.Enum, Name: var en })
            {
                EmitAssign(ReadEnumExpr(en, bufferVar));
                return;
            }

            // Default: readRaw<T>()
            EmitAssign(ReadExprInternal(t, bufferVar));
        }
    }
}
