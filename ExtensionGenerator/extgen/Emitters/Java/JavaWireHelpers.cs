using codegencore.Writers.Lang;
using extgen.Bridge;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem.Java;

namespace extgen.Emitters.Java
{
    internal class JavaWireHelpers(RuntimeNaming runtime, JavaTypeMap typeMap) : WireHelpersBase<JavaWriter>
    {
        // ============================================================
        // Core scalar read / write helpers
        // ============================================================

        /// <summary>
        /// Read a scalar value (including float/double/string) from the buffer.
        /// Used for *general* scalars, not enums.
        /// </summary>
        private static string ReadScalarCore(IrType t, string buf, string wire) =>
            t.Name switch
            {
                "bool" => $"{wire}.readBool({buf})",
                "int8" => $"{wire}.readI8({buf})",
                "uint8" => $"{wire}.readI8({buf})",
                "int16" => $"{wire}.readI16({buf})",
                "uint16" => $"{wire}.readI16({buf})",
                "int32" => $"{wire}.readI32({buf})",
                "uint32" => $"{wire}.readI32({buf})",
                "int64" => $"{wire}.readI64({buf})",
                "uint64" => $"{wire}.readI64({buf})",
                "float" => $"{wire}.readF32({buf})",
                "double" => $"{wire}.readF64({buf})",
                "string" => $"{wire}.readString({buf})",
                _ => throw new NotSupportedException($"read unsupported scalar {t.Name}")
            };

        /// <summary>
        /// Write a scalar value (including float/double/string) into the buffer.
        /// Used for *general* scalars, not enums.
        /// </summary>
        private static string WriteScalarCore(IrType t, string buf, string val, string wire) =>
            t.Name switch
            {
                "bool" => $"{wire}.writeBool({buf}, {val})",
                "int8" => $"{wire}.writeI8({buf}, {val})",
                "uint8" => $"{wire}.writeI8({buf}, {val})",
                "int16" => $"{wire}.writeI16({buf}, {val})",
                "uint16" => $"{wire}.writeI16({buf}, {val})",
                "int32" => $"{wire}.writeI32({buf}, {val})",
                "uint32" => $"{wire}.writeI32({buf}, {val})",
                "int64" => $"{wire}.writeI64({buf}, {val})",
                "uint64" => $"{wire}.writeI64({buf}, {val})",
                "float" => $"{wire}.writeF32({buf}, {val})",
                "double" => $"{wire}.writeF64({buf}, {val})",
                "string" => $"{wire}.writeString({buf}, {val})",
                _ => throw new NotSupportedException($"write unsupported scalar {t.Name}")
            };

        // ============================================================
        // Integer-only scalar helpers (for enum underlying types)
        // ============================================================

        /// <summary>
        /// Read an integer-like scalar (bool/int*/uint*) from the buffer.
        /// Used strictly for enum underlying values. Will throw for float/double/string.
        /// </summary>
        private static string ReadScalarIntegerOnly(IrType t, string buf, string wire) =>
            t.Name switch
            {
                "bool" => $"{wire}.readBool({buf})",
                "int8" => $"{wire}.readI8({buf})",
                "uint8" => $"{wire}.readI8({buf})",
                "int16" => $"{wire}.readI16({buf})",
                "uint16" => $"{wire}.readI16({buf})",
                "int32" => $"{wire}.readI32({buf})",
                "uint32" => $"{wire}.readI32({buf})",
                "int64" => $"{wire}.readI64({buf})",
                "uint64" => $"{wire}.readI64({buf})",
                _ => throw new NotSupportedException(
                        $"Invalid enum underlying type for Java: {t.Name}")
            };

        /// <summary>
        /// Write an integer-like scalar (bool/int*/uint*) into the buffer.
        /// Used strictly for enum underlying values. Will throw for float/double/string.
        /// </summary>
        private static string WriteScalarIntegerOnly(IrType t, string buf, string val, string wire) =>
            t.Name switch
            {
                "bool" => $"{wire}.writeBool({buf}, {val})",
                "int8" => $"{wire}.writeI8({buf}, {val})",
                "uint8" => $"{wire}.writeI8({buf}, {val})",
                "int16" => $"{wire}.writeI16({buf}, {val})",
                "uint16" => $"{wire}.writeI16({buf}, {val})",
                "int32" => $"{wire}.writeI32({buf}, {val})",
                "uint32" => $"{wire}.writeI32({buf}, {val})",
                "int64" => $"{wire}.writeI64({buf}, {val})",
                "uint64" => $"{wire}.writeI64({buf}, {val})",
                _ => throw new NotSupportedException(
                        $"Invalid enum underlying type for Java: {t.Name}")
            };

        private static string ReadUnderlying(IrType u, string buf, string wire) =>
            ReadScalarIntegerOnly(u, buf, wire);

        private static string WriteUnderlying(IrType u, string buf, string val, string wire) =>
            WriteScalarIntegerOnly(u, buf, val, wire);

        // ============================================================
        // Read / Write expressions (non-collection)
        // ============================================================

        public override string ReadExpr(IrType t, string bufferVar)
        {
            var wire = runtime.WireClass;

            if (t.Kind == IrTypeKind.Enum && t.Underlying is not null)
                return $"{t.Name}.from({ReadUnderlying(t.Underlying, bufferVar, wire)})";

            if (t.Kind == IrTypeKind.Struct)
                return $"{t.Name}Codec.read({bufferVar})";

            if (t.Kind == IrTypeKind.Scalar)
                return ReadScalarCore(t, bufferVar, wire);

            if (t.Kind == IrTypeKind.Function)
                return $"{wire}.readGMFunction({bufferVar}, {runtime.DispatchQueueField})";

            if (t.Kind == IrTypeKind.Any)
                return $"{wire}.readGMValue({bufferVar})";

            if (t.Kind == IrTypeKind.AnyArray)
                return $"{wire}.readGMArray({bufferVar})";

            if (t.Kind == IrTypeKind.AnyMap)
                return $"{wire}.readGMObject({bufferVar})";

            if (t.Kind == IrTypeKind.Buffer)
                return $"{runtime.BufferQueueField}.poll()";

            throw new NotSupportedException($"read unsupported kind {t.Kind}");
        }

        public override string WriteExpr(IrType t, string bufferVar, string valueExpr)
        {
            var wire = runtime.WireClass;

            if (t.Kind == IrTypeKind.Enum && t.Underlying is not null)
                return WriteUnderlying(t.Underlying, bufferVar, $"{valueExpr}.value()", wire);

            if (t.Kind == IrTypeKind.Struct)
                return $"{t.Name}Codec.write({bufferVar}, {valueExpr})";

            if (t.Kind == IrTypeKind.Scalar)
                return WriteScalarCore(t, bufferVar, valueExpr, wire);

            if (t.Kind == IrTypeKind.Any)
                return $"{wire}.writeGMValue({bufferVar}, {valueExpr})";

            if (t.Kind == IrTypeKind.AnyArray)
                return $"{wire}.writeGMArray({bufferVar}, {valueExpr})";

            if (t.Kind == IrTypeKind.AnyMap)
                return $"{wire}.writeGMObject({bufferVar}, {valueExpr})";

            throw new NotSupportedException($"write unsupported kind {t.Kind}");
        }

        // ============================================================
        // High-level decode / encode helpers (CStyleWriter-friendly)
        // ============================================================

        /// <summary>
        /// Decode a value of IrType t from the ByteBuffer into "accessor".
        /// Uses IIrTypeMap for the Java declared type (including Optional/List/arrays).
        /// </summary>
        public override void DecodeLines(JavaWriter w, IrType t, string accessor, bool declare, string bufferVar)
        {
            var javaType = typeMap.Map(t);

            // Nullable -> Optional<T> with leading presence bool
            if (t.IsNullable)
            {
                w.Assign(accessor, "java.util.Optional.empty()", declare ? javaType : null);

                w.If($"{runtime.WireClass}.readBool({bufferVar})", then =>
                {
                    string tmp = $"__opt_{accessor}";

                    var inner = t with { IsNullable = false };
                    DecodeLines(then, inner, tmp, true, bufferVar);
                    then.Line($"{accessor} = java.util.Optional.of({tmp});");
                });

                return;
            }

            // Collections: fixed-length -> T[], dynamic -> List<T>
            if (t.IsCollection)
            {
                var el = IrHelpers.Element(t);
                var elJavaType = typeMap.Map(el);

                if (t.FixedLength is int n)
                {
                    string arrInit = $"new {elJavaType}[{n}]";
                    w.Assign(
                        accessor,
                        $"{runtime.WireClass}.readFixedArray({bufferVar}, {n}, bb -> {ReadExpr(el, "bb")}, {arrInit})",
                        declare ? javaType : null);
                }
                else
                {
                    w.Assign(
                        accessor,
                        $"{runtime.WireClass}.readList({bufferVar}, bb -> {ReadExpr(el, "bb")})",
                        declare ? javaType : null);
                }

                return;
            }

            // Plain scalar / enum / struct / function / any / buffer
            w.Assign(accessor, ReadExpr(t, bufferVar), declare ? javaType : null);
        }

        /// <summary>
        /// Encode a value of IrType t from "accessor" into the ByteBuffer.
        /// </summary>
        public override void EncodeLines(JavaWriter w, IrType t, string accessor, string bufVar)
        {
            var wireClass = runtime.WireClass;

            // Nullable -> presence bool + payload if present
            if (t.IsNullable)
            {
                var inner = t with { IsNullable = false };
                w.Line($"{wireClass}.writeBool({bufVar}, {accessor} != null && {accessor}.isPresent());");
                w.If($"{accessor} != null && {accessor}.isPresent()",
                    then => EncodeLines(then, inner, $"{accessor}.get()", bufVar));
                return;
            }

            // Collections
            if (t.IsCollection)
            {
                var el = IrHelpers.Element(t);
                var lambda = $"(bb, x) -> {WriteExpr(el, "bb", "x")}";

                if (t.FixedLength is int)
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

        /// <summary>
        /// Map an enum's underlying IrType into the Java primitive type used
        /// for the backing field in the generated enum (e.g. int, long, boolean).
        /// Only integral-like types are allowed here.
        /// </summary>
        public static string ScalarForEnum(IrType underlying) =>
            underlying.Name switch
            {
                "bool" => "boolean",
                "int8" => "byte",
                "uint8" => "byte",
                "int16" => "short",
                "uint16" => "short",
                "int32" => "int",
                "uint32" => "int",
                "int64" => "long",
                "uint64" => "long",
                _ => throw new NotSupportedException(
                        $"Invalid enum underlying type for Java: {underlying.Name}")
            };
    }
}
