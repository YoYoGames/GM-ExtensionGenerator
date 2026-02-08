using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.TypeSystem.Cpp;

namespace extgen.Emitters.Cpp
{
    internal sealed class CppWireHelpers<TWriter>(
        RuntimeNaming runtime,
        CppTypeMap typeMap,
        IIrTypeEnumResolver enums,
        bool typedEnums = true
    ) : WireHelpersBase<TWriter>
        where TWriter : CxxWriter<TWriter>
    {
        private readonly RuntimeNaming _runtime = runtime;
        private readonly CppTypeMap _typeMap = typeMap;
        private readonly IIrTypeEnumResolver _enums = enums;
        private readonly bool _typedEnums = typedEnums;

        // ============================================================
        //  Low-level read / write expression helpers (non-nullable, non-array)
        // ============================================================

        private string ReadNonWrapped(IrType t, string bufferVar, bool owned)
        {
            var ns = _runtime.CodeGenNamespace;

            // Enum
            if (t is IrType.Named { Kind: NamedKind.Enum, Name: var enumName })
            {
                if (_typedEnums)
                {
                    var cppEnum = _typeMap.Map(t, owned: owned);
                    return $"{ns}::readValue<{cppEnum}>({bufferVar})";
                }
                else
                {
                    var u = _enums.GetUnderlying(enumName);
                    var uCpp = _typeMap.Map(u, owned: false);
                    return $"{ns}::readValue<{uCpp}>({bufferVar})";
                }
            }

            // Buffer (special queue)
            if (t is IrType.Builtin { Kind: BuiltinKind.Buffer })
            {
                return $"{_runtime.BufferQueueField}.front()";
            }

            // Function (special read helper)
            if (t is IrType.Builtin { Kind: BuiltinKind.Function })
            {
                return $"{ns}::readFunction({bufferVar}, &{_runtime.DispatchQueueField})";
            }

            // Everything else
            var cpp = _typeMap.Map(t, owned: owned);
            return $"{ns}::readValue<{cpp}>({bufferVar})";
        }

        private string WriteNonWrapped(IrType t, string bufferVar, string valueExpr)
        {
            var ns = _runtime.CodeGenNamespace;

            if (t is IrType.Builtin { Kind: BuiltinKind.Buffer })
                throw new NotSupportedException("code emitter: buffers as return values are not supported.");

            if (t is IrType.Builtin { Kind: BuiltinKind.Function })
                throw new NotSupportedException("code emitter: functions as return values are not supported.");

            // NOTE: if typedEnums=false and caller passes enum type, you must cast/extract underlying
            // somewhere else. In practice, typedEnums=false is usually a *decode* policy (Swift).
            return $"{ns}::writeValue({bufferVar}, {valueExpr})";
        }

        // ============================================================
        //  WireHelpersBase overrides (expression helpers)
        // ============================================================

        public override string ReadExpr(IrType t, string bufferVar)
        {
            if (t is IrType.Nullable or IrType.Array)
                throw new NotSupportedException("ReadExpr expects a non-nullable, non-array IrType. Use DecodeLines.");
            return ReadNonWrapped(t, bufferVar, owned: true);
        }

        public override string WriteExpr(IrType t, string bufferVar, string valueExpr)
        {
            if (t is IrType.Nullable or IrType.Array)
                throw new NotSupportedException("WriteExpr expects a non-nullable, non-array IrType. Use EncodeLines.");
            return WriteNonWrapped(t, bufferVar, valueExpr);
        }

        public override void DecodeLines(TWriter w, IrType t, string accessor, bool declare, string bufferVar)
            => DecodeLines(w, t, accessor, declare, bufferVar, owned: true);

        public override void EncodeLines(TWriter w, IrType t, string accessor, string bufferVar)
        {
            var ns = _runtime.CodeGenNamespace;

            // Policy: don’t allow Buffer/Function nested under wrappers if you consider them illegal.
            if (ContainsBuiltin(t, BuiltinKind.Buffer))
                throw new NotSupportedException("code emitter: buffers in return values are not supported.");

            if (ContainsBuiltin(t, BuiltinKind.Function))
                throw new NotSupportedException("code emitter: functions in return values are not supported.");

            // Your runtime already supports writeValue for optional/vector/array etc.
            w.Call($"{ns}::writeValue", bufferVar, accessor).Line(";");
        }

        // ============================================================
        //  DecodeLines with ownership control
        // ============================================================

        public void DecodeLines(
            TWriter w,
            IrType t,
            string accessor,
            bool declare,
            string bufferVar,
            bool owned)
        {
            var ns = _runtime.CodeGenNamespace;
            string? declType = declare ? _typeMap.Map(t, owned: owned) : null;

            // Nullable -> std::optional<T>
            if (t is IrType.Nullable n)
            {
                var inner = n.Underlying;

                // If you have a special-case optional layout for function, keep it.
                // Otherwise just readOptional<T>.
                if (ContainsBuiltin(inner, BuiltinKind.Function))
                {
                    if (declare)
                        w.Declare(declType!, accessor, "std::nullopt", initStyle: InitStyle.Equals);

                    w.If($"{ns}::readValue<bool>({bufferVar})", thenBody =>
                    {
                        DecodeLines(thenBody, inner, accessor, declare: false, bufferVar, owned);
                    });

                    return;
                }

                w.Assign(
                    accessor,
                    $"{ns}::readOptional<{_typeMap.Map(inner, owned: owned)}>({bufferVar})",
                    declType);

                return;
            }

            // Array -> std::array / std::vector
            if (t is IrType.Array a)
            {
                var el = a.Element;
                var elCpp = _typeMap.Map(el, owned: owned);

                if (a.FixedLength is int nFixed)
                {
                    w.Assign(
                        accessor,
                        $"{ns}::readArray<{elCpp}, {nFixed}>({bufferVar})",
                        declType);
                }
                else
                {
                    w.Assign(
                        accessor,
                        $"{ns}::readVector<{elCpp}>({bufferVar})",
                        declType);
                }

                return;
            }

            // Buffer: consume queue
            if (t is IrType.Builtin { Kind: BuiltinKind.Buffer })
            {
                w.Assign(accessor, ReadNonWrapped(t, bufferVar, owned), declType);
                w.Line($"{_runtime.BufferQueueField}.pop();");
                return;
            }

            // Enum
            if (t is IrType.Named { Kind: NamedKind.Enum, Name: var enumName })
            {
                if (_typedEnums)
                {
                    w.Assign(accessor, ReadNonWrapped(t, bufferVar, owned), declType);
                }
                else
                {
                    var u = _enums.GetUnderlying(enumName);
                    var uCpp = _typeMap.Map(u, owned: false);
                    w.Assign(accessor, $"{ns}::readValue<{uCpp}>({bufferVar})", declare ? uCpp : null);
                }
                return;
            }

            // Default
            w.Assign(accessor, ReadNonWrapped(t, bufferVar, owned), declType);
        }

        private static bool ContainsBuiltin(IrType t, BuiltinKind kind) =>
            t switch
            {
                IrType.Builtin b => b.Kind == kind,
                IrType.Nullable n => ContainsBuiltin(n.Underlying, kind),
                IrType.Array a => ContainsBuiltin(a.Element, kind),
                _ => false
            };
    }
}
