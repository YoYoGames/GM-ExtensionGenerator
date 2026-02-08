using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.TypeSystem.Java;

namespace extgen.Emitters.Android.Java
{
    internal sealed class JavaWireHelpers : WireHelpersBase<JavaWriter>
    {
        private readonly RuntimeNaming _runtime;
        private readonly JavaTypeMap _typeMap;
        private readonly IIrTypeEnumResolver _enums;

        public JavaWireHelpers(RuntimeNaming runtime, JavaTypeMap typeMap, IIrTypeEnumResolver enums)
        {
            _runtime = runtime;
            _typeMap = typeMap;
            _enums = enums;
        }

        // ============================================================
        // Core scalar read / write helpers (Builtins only)
        // ============================================================

        private static bool IsScalarBuiltin(BuiltinKind k) => k is
            BuiltinKind.Bool or
            BuiltinKind.Int8 or BuiltinKind.UInt8 or
            BuiltinKind.Int16 or BuiltinKind.UInt16 or
            BuiltinKind.Int32 or BuiltinKind.UInt32 or
            BuiltinKind.Int64 or BuiltinKind.UInt64 or
            BuiltinKind.Float32 or BuiltinKind.Float64 or
            BuiltinKind.String;

        private static bool IsIntegerLikeBuiltin(BuiltinKind k) => k is
            BuiltinKind.Bool or
            BuiltinKind.Int8 or BuiltinKind.UInt8 or
            BuiltinKind.Int16 or BuiltinKind.UInt16 or
            BuiltinKind.Int32 or BuiltinKind.UInt32 or
            BuiltinKind.Int64 or BuiltinKind.UInt64;

        /// <summary>
        /// Read a scalar value (including float/double/string) from the buffer.
        /// Used for general scalars, not enums.
        /// </summary>
        private static string ReadScalarCore(BuiltinKind k, string buf, string wire) => k switch
        {
            BuiltinKind.Bool => $"{wire}.readBool({buf})",
            BuiltinKind.Int8 => $"{wire}.readI8({buf})",
            BuiltinKind.UInt8 => $"{wire}.readI8({buf})",
            BuiltinKind.Int16 => $"{wire}.readI16({buf})",
            BuiltinKind.UInt16 => $"{wire}.readI16({buf})",
            BuiltinKind.Int32 => $"{wire}.readI32({buf})",
            BuiltinKind.UInt32 => $"{wire}.readI32({buf})",
            BuiltinKind.Int64 => $"{wire}.readI64({buf})",
            BuiltinKind.UInt64 => $"{wire}.readI64({buf})",
            BuiltinKind.Float32 => $"{wire}.readF32({buf})",
            BuiltinKind.Float64 => $"{wire}.readF64({buf})",
            BuiltinKind.String => $"{wire}.readString({buf})",
            _ => throw new NotSupportedException($"read unsupported scalar builtin {k}")
        };

        /// <summary>
        /// Write a scalar value (including float/double/string) into the buffer.
        /// Used for general scalars, not enums.
        /// </summary>
        private static string WriteScalarCore(BuiltinKind k, string buf, string val, string wire) => k switch
        {
            BuiltinKind.Bool => $"{wire}.writeBool({buf}, {val})",
            BuiltinKind.Int8 => $"{wire}.writeI8({buf}, {val})",
            BuiltinKind.UInt8 => $"{wire}.writeI8({buf}, {val})",
            BuiltinKind.Int16 => $"{wire}.writeI16({buf}, {val})",
            BuiltinKind.UInt16 => $"{wire}.writeI16({buf}, {val})",
            BuiltinKind.Int32 => $"{wire}.writeI32({buf}, {val})",
            BuiltinKind.UInt32 => $"{wire}.writeI32({buf}, {val})",
            BuiltinKind.Int64 => $"{wire}.writeI64({buf}, {val})",
            BuiltinKind.UInt64 => $"{wire}.writeI64({buf}, {val})",
            BuiltinKind.Float32 => $"{wire}.writeF32({buf}, {val})",
            BuiltinKind.Float64 => $"{wire}.writeF64({buf}, {val})",
            BuiltinKind.String => $"{wire}.writeString({buf}, {val})",
            _ => throw new NotSupportedException($"write unsupported scalar builtin {k}")
        };

        // ============================================================
        // Integer-only scalar helpers (for enum underlying types)
        // ============================================================

        private static string ReadScalarIntegerOnly(BuiltinKind k, string buf, string wire) => k switch
        {
            BuiltinKind.Bool => $"{wire}.readBool({buf})",
            BuiltinKind.Int8 => $"{wire}.readI8({buf})",
            BuiltinKind.UInt8 => $"{wire}.readI8({buf})",
            BuiltinKind.Int16 => $"{wire}.readI16({buf})",
            BuiltinKind.UInt16 => $"{wire}.readI16({buf})",
            BuiltinKind.Int32 => $"{wire}.readI32({buf})",
            BuiltinKind.UInt32 => $"{wire}.readI32({buf})",
            BuiltinKind.Int64 => $"{wire}.readI64({buf})",
            BuiltinKind.UInt64 => $"{wire}.readI64({buf})",
            _ => throw new NotSupportedException($"Invalid enum underlying builtin for Java: {k}")
        };

        private static string WriteScalarIntegerOnly(BuiltinKind k, string buf, string val, string wire) => k switch
        {
            BuiltinKind.Bool => $"{wire}.writeBool({buf}, {val})",
            BuiltinKind.Int8 => $"{wire}.writeI8({buf}, {val})",
            BuiltinKind.UInt8 => $"{wire}.writeI8({buf}, {val})",
            BuiltinKind.Int16 => $"{wire}.writeI16({buf}, {val})",
            BuiltinKind.UInt16 => $"{wire}.writeI16({buf}, {val})",
            BuiltinKind.Int32 => $"{wire}.writeI32({buf}, {val})",
            BuiltinKind.UInt32 => $"{wire}.writeI32({buf}, {val})",
            BuiltinKind.Int64 => $"{wire}.writeI64({buf}, {val})",
            BuiltinKind.UInt64 => $"{wire}.writeI64({buf}, {val})",
            _ => throw new NotSupportedException($"Invalid enum underlying builtin for Java: {k}")
        };

        private static string ReadUnderlying(IrType underlying, string buf, string wire)
        {
            underlying = IrType.StripNullable(underlying);

            if (underlying is not IrType.Builtin b || !IsIntegerLikeBuiltin(b.Kind))
                throw new NotSupportedException($"Invalid enum underlying type for Java: {underlying}");

            return ReadScalarIntegerOnly(b.Kind, buf, wire);
        }

        private static string WriteUnderlying(IrType underlying, string buf, string val, string wire)
        {
            underlying = IrType.StripNullable(underlying);

            if (underlying is not IrType.Builtin b || !IsIntegerLikeBuiltin(b.Kind))
                throw new NotSupportedException($"Invalid enum underlying type for Java: {underlying}");

            return WriteScalarIntegerOnly(b.Kind, buf, val, wire);
        }

        private IrType GetEnumUnderlyingOrThrow(string enumName)
        {
            if (!_enums.TryGetUnderlying(enumName, out var underlying))
                throw new NotSupportedException($"Enum underlying type not found for '{enumName}'.");
            return underlying;
        }

        // ============================================================
        // Read / Write expressions (non-collection)
        // ============================================================

        public override string ReadExpr(IrType t, string bufferVar)
        {
            var wire = _runtime.WireClass;

            // DecodeLines handles Optional + arrays. Here we focus on the "atomic" read.
            t = IrType.StripNullable(t);

            return t switch
            {
                // Enum: read underlying then convert
                IrType.Named { Kind: NamedKind.Enum, Name: var name } =>
                    $"{name}.from({ReadUnderlying(GetEnumUnderlyingOrThrow(name), bufferVar, wire)})",

                // Struct: codec
                IrType.Named { Kind: NamedKind.Struct, Name: var st } =>
                    $"{st}Codec.read({bufferVar})",

                // Builtin scalar-ish
                IrType.Builtin { Kind: var k } when IsScalarBuiltin(k) =>
                    ReadScalarCore(k, bufferVar, wire),

                // Special builtins
                IrType.Builtin { Kind: BuiltinKind.Function } =>
                    $"{wire}.readGMFunction({bufferVar}, {_runtime.DispatchQueueField})",

                IrType.Builtin { Kind: BuiltinKind.Any } =>
                    $"{wire}.readGMValue({bufferVar})",

                IrType.Builtin { Kind: BuiltinKind.AnyArray } =>
                    $"{wire}.readGMArray({bufferVar})",

                IrType.Builtin { Kind: BuiltinKind.AnyMap } =>
                    $"{wire}.readGMObject({bufferVar})",

                IrType.Builtin { Kind: BuiltinKind.Buffer } =>
                    $"{_runtime.BufferQueueField}.poll()",

                _ => throw new NotSupportedException($"read unsupported type {t}")
            };
        }

        public override string WriteExpr(IrType t, string bufferVar, string valueExpr)
        {
            var wire = _runtime.WireClass;

            // EncodeLines handles Optional + arrays. Here we focus on the "atomic" write.
            t = IrType.StripNullable(t);

            return t switch
            {
                // Enum: write underlying of .value()
                IrType.Named { Kind: NamedKind.Enum, Name: var name } =>
                    WriteUnderlying(GetEnumUnderlyingOrThrow(name), bufferVar, $"{valueExpr}.value()", wire),

                // Struct: codec
                IrType.Named { Kind: NamedKind.Struct, Name: var st } =>
                    $"{st}Codec.write({bufferVar}, {valueExpr})",

                // Builtin scalar-ish
                IrType.Builtin { Kind: var k } when IsScalarBuiltin(k) =>
                    WriteScalarCore(k, bufferVar, valueExpr, wire),

                // Special builtins
                IrType.Builtin { Kind: BuiltinKind.Any } =>
                    $"{wire}.writeGMValue({bufferVar}, {valueExpr})",

                IrType.Builtin { Kind: BuiltinKind.AnyArray } =>
                    $"{wire}.writeGMArray({bufferVar}, {valueExpr})",

                IrType.Builtin { Kind: BuiltinKind.AnyMap } =>
                    $"{wire}.writeGMObject({bufferVar}, {valueExpr})",

                _ => throw new NotSupportedException($"write unsupported type {t}")
            };
        }

        // ============================================================
        // High-level decode / encode helpers (CStyleWriter-friendly)
        // ============================================================

        public override void DecodeLines(JavaWriter w, IrType t, string accessor, bool declare, string bufferVar)
        {
            var javaType = _typeMap.Map(t);

            // Nullable -> Optional<T> with leading presence bool
            if (IrType.IsNullable(t))
            {
                w.Assign(accessor, "java.util.Optional.empty()", declare ? javaType : null);

                w.If($"{_runtime.WireClass}.readBool({bufferVar})", then =>
                {
                    var inner = IrType.StripNullable(t);
                    var tmp = $"__opt_{accessor}";

                    DecodeLines(then, inner, tmp, true, bufferVar);
                    then.Line($"{accessor} = java.util.Optional.of({tmp});");
                });

                return;
            }

            // Arrays: fixed-length -> T[], dynamic -> List<T>
            t = IrType.StripNullable(t);

            if (t is IrType.Array a)
            {
                var el = a.Element;
                var elJavaType = _typeMap.Map(el);

                if (a.FixedLength is int n)
                {
                    string arrInit = $"new {elJavaType}[{n}]";
                    w.Assign(
                        accessor,
                        $"{_runtime.WireClass}.readFixedArray({bufferVar}, {n}, bb -> {ReadExpr(el, "bb")}, {arrInit})",
                        declare ? javaType : null);
                }
                else
                {
                    w.Assign(
                        accessor,
                        $"{_runtime.WireClass}.readList({bufferVar}, bb -> {ReadExpr(el, "bb")})",
                        declare ? javaType : null);
                }

                return;
            }

            // Plain scalar / enum / struct / function / any / buffer
            w.Assign(accessor, ReadExpr(t, bufferVar), declare ? javaType : null);
        }

        public override void EncodeLines(JavaWriter w, IrType t, string accessor, string bufVar)
        {
            var wireClass = _runtime.WireClass;

            // Nullable -> presence bool + payload if present
            if (IrType.IsNullable(t))
            {
                var inner = IrType.StripNullable(t);
                w.Line($"{wireClass}.writeBool({bufVar}, {accessor} != null && {accessor}.isPresent());");
                w.If($"{accessor} != null && {accessor}.isPresent()",
                    then => EncodeLines(then, inner, $"{accessor}.get()", bufVar));
                return;
            }

            t = IrType.StripNullable(t);

            // Arrays
            if (t is IrType.Array a)
            {
                var el = a.Element;
                var lambda = $"(bb, x) -> {WriteExpr(el, "bb", "x")}";

                if (a.FixedLength is int)
                    w.Call($"{wireClass}.writeFixedArray", bufVar, accessor, lambda).Line(";");
                else
                    w.Call($"{wireClass}.writeList", bufVar, accessor, lambda).Line(";");

                return;
            }

            // Plain scalar / enum / struct / any
            w.Line($"{WriteExpr(t, bufVar, accessor)};");
        }

        // ============================================================
        // Helper for enum field type in generated Java
        // ============================================================

        public static string ScalarForEnum(IrType underlying)
        {
            underlying = IrType.StripNullable(underlying);

            if (underlying is not IrType.Builtin b || !IsIntegerLikeBuiltin(b.Kind))
                throw new NotSupportedException($"Invalid enum underlying type for Java: {underlying}");

            return b.Kind switch
            {
                // signed
                BuiltinKind.Int8 => "byte",
                BuiltinKind.Int16 => "short",
                BuiltinKind.Int32 => "int",

                // unsigned -> int
                BuiltinKind.UInt8 => "int",
                BuiltinKind.UInt16 => "int",
                BuiltinKind.UInt32 => "int",

                // hard NO
                BuiltinKind.Int64 or BuiltinKind.UInt64 =>
                    throw new NotSupportedException(
                        "Java enums cannot use 64-bit underlying types"),

                _ => throw new NotSupportedException($"Invalid enum underlying type for Java: {b.Kind}")
            };
        }
    }
}
