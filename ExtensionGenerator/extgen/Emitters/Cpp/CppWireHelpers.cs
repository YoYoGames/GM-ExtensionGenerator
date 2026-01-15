using codegencore.Writers.Lang;
using extgen.Bridge;
using extgen.Model;
using extgen.Options;
using extgen.TypeSystem.Cpp;

namespace extgen.Emitters.Cpp
{
    /// <summary>
    /// C++ wire helper used by desktop, ObjC, Swift bridges.
    ///
    /// Policy knobs:
    ///   - typedEnums: if true, decode enums as their enum type;
    ///                 if false, decode as underlying integer type (Swift-friendly).
    ///   - "owned" (per-call): controls Map(t, owned):
    ///         owned=true  -> std::string / std::vector / etc
    ///         owned=false -> std::string_view / span / etc (cheap views)
    ///
    /// This class is wired to the shared bridge abstraction via
    /// WireHelpersBase&lt;TWriter&gt;, using TWriter constrained
    /// to CxxWriter&lt;TWriter&gt;.
    /// </summary>
    internal sealed class CppWireHelpers<TWriter>(
        RuntimeNaming runtime,
        CppTypeMap typeMap
    ) : WireHelpersBase<TWriter>
        where TWriter : CxxWriter<TWriter>
    {
        // ============================================================
        //  Low-level read / write expression helpers (non-collection)
        // ============================================================

        private string ReadEnumExpr(IrType t, string bufferVar, bool owned, out string returnType)
        {
            var ns = runtime.CodeGenNamespace;
            
            returnType = typeMap.Map(t, owned: owned);

            // ObjC / C++ flavor: cast to the actual enum type.
            return $"{ns}::readValue<{returnType}>({bufferVar})";
        }

        private string ReadExprInternal(IrType t, string bufferVar, bool owned)
        {
            var ns = runtime.CodeGenNamespace;

            if (t.IsNullable || t.IsCollection)
                throw new NotSupportedException("ReadExprInternal expects a non-nullable, non-collection type.");

            if (t.Kind == IrTypeKind.Enum && t.Underlying is not null)
                return ReadEnumExpr(t, bufferVar, owned, out string _);

            if (t.Kind == IrTypeKind.Buffer)
            {
                // For “raw” expression, just fetch; caller pops if needed.
                return $"{runtime.BufferQueueField}.front()";
            }

            if (t.Kind == IrTypeKind.Function)
            {
                return $"{ns}::readFunction({bufferVar}, &{runtime.DispatchQueueField})";
            }

            // Plain scalar/struct/any/variant/etc: use unified readValue<T>.
            var cpp = typeMap.Map(t, owned: owned);
            return $"{ns}::readValue<{cpp}>({bufferVar})";
        }

        private string WriteExprInternal(IrType t, string bufferVar, string valueExpr)
        {
            var ns = runtime.CodeGenNamespace;

            if (t.IsNullable || t.IsCollection)
                throw new NotSupportedException("WriteExprInternal expects a non-nullable, non-collection type.");

            if (t.Kind == IrTypeKind.Buffer)
            {
                throw new NotSupportedException("code emitter: buffers as return values are not supported.");
            }

            if (t.Kind == IrTypeKind.Function)
            {
                throw new NotSupportedException("code emitter: functions as return values are not supported.");
            }

            // Plain scalar/struct/any/etc
            return $"{ns}::writeValue({bufferVar}, {valueExpr})";
        }

        // ============================================================
        //  WireHelpersBase overrides
        //  (default to owned values for the scalar case)
        // ============================================================

        public override string ReadExpr(IrType t, string bufferVar)
            => ReadExprInternal(t, bufferVar, owned: true);

        public override string WriteExpr(IrType t, string bufferVar, string valueExpr)
            => WriteExprInternal(t, bufferVar, valueExpr);

        /// <summary>
        /// Default "DecodeLines" used by generic bridge code:
        /// decodes as owned types (std::string, std::vector, etc).
        /// </summary>
        public override void DecodeLines(
            TWriter w,
            IrType t,
            string accessor,
            bool declare,
            string bufferVar)
            => DecodeLines(w, t, accessor, declare, bufferVar, owned: true);

        /// <summary>
        /// EncodeLines is fully specified by type + accessor.
        /// No ownership policy needed on the encode side.
        /// </summary>
        public override void EncodeLines(
            TWriter w,
            IrType t,
            string accessor,
            string bufferVar)
        {
            var ns = runtime.CodeGenNamespace;

            // Nullable -> presence bool + payload if present
            if (t.IsNullable)
            {
                w.Call($"{ns}::writeValue", bufferVar, accessor).Line(";");
                return;
            }

            // Collections
            if (t.IsCollection)
            {
                w.Call($"{ns}::writeValue", bufferVar, accessor).Line(";");
                return;
            }

            // Everything else: delegate to scalar/enum helper
            w.Line(WriteExprInternal(t, bufferVar, accessor) + ";");
        }

        // ============================================================
        //  High-level DecodeLines with ownership control
        // ============================================================

        /// <summary>
        /// Decode a value of IrType t from the buffer into "accessor".
        /// "owned" controls whether we map t to owned storage (std::string)
        /// or view types (std::string_view), via CppTypeMap.Map(t, owned).
        /// </summary>
        public void DecodeLines(
            TWriter w,
            IrType t,
            string accessor,
            bool declare,
            string bufferVar,
            bool owned)
        {
            var ns = runtime.CodeGenNamespace;

            // This is the “intended” C++ type for the IR type.
            string? typeForDecl = declare ? typeMap.Map(t, owned: owned) : null;

            // Nullable -> std::optional<T>
            if (t.IsNullable)
            {
                var inner = t with { IsNullable = false };
                if (t.Kind == IrTypeKind.Function)
                {
                    if (declare)
                    {
                        w.Declare(typeForDecl!, accessor, "std::nullopt", initStyle: InitStyle.Equals);
                        w.If($"{ns}::readValue<bool>({bufferVar})", thenBody => 
                        {
                            DecodeLines(w, inner, accessor, false, bufferVar, owned);
                        });
                    }
                }
                else
                {
                    w.Assign(accessor, $"{ns}::readOptional<{typeMap.Map(inner, owned: owned)}>({bufferVar})", typeForDecl);
                }
                return;
            }

            // Collections
            if (t.IsCollection)
            {
                var el = IrHelpers.Element(t);
                var elType = typeMap.Map(el, owned: true); // elements usually owned in container

                if (t.FixedLength is int n)
                {
                    w.Assign(accessor, $"{ns}::readArray<{elType}, {n}>({bufferVar})", typeForDecl);
                }
                else
                {
                    w.Assign(accessor, $"{ns}::readVector<{elType}>({bufferVar})", typeForDecl);
                }
                return;
            }

            // Buffer: consume from queue
            if (t.Kind == IrTypeKind.Buffer)
            {
                // Here we *know* we want the queue type, not the wire type.
                var expr = ReadExprInternal(t, bufferVar, owned);
                w.Assign(accessor, expr, typeForDecl);
                w.Line($"{runtime.BufferQueueField}.pop();");
                return;
            }

            // Enum (where Swift vs C++ differ in the *declared* type)
            if (t.Kind == IrTypeKind.Enum && t.Underlying is not null)
            {
                var readExpr = ReadEnumExpr(t, bufferVar, owned, out string returnType);
                w.Assign(accessor, readExpr, declare ? returnType : null);

                return;
            }

            // Plain scalar / struct / any / function
            var valueExpr = ReadExprInternal(t, bufferVar, owned);
            w.Assign(accessor, valueExpr, typeForDecl);
        }
    }
}
