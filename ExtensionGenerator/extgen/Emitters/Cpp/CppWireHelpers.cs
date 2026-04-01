using codegencore.Models;
using codegencore.Writers.Lang;
using extgen.Bridge;
using extgen.Models.Config;
using extgen.Models.Utils;
using extgen.TypeSystem.Cpp;

namespace extgen.Emitters.Cpp
{
    /// <summary>
    /// Provides wire protocol encoding/decoding helpers for C++ code generation,
    /// handling scalars, enums, structs, arrays, and optionals.
    /// </summary>
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

        private string ReadNonWrapped(IrType t, string bufferVar, bool owned)
        {
            var ns = _runtime.CodeGenNamespace;

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

            if (t is IrType.Builtin { Kind: BuiltinKind.Buffer })
            {
                return $"{_runtime.BufferQueueField}.front()";
            }

            if (t is IrType.Builtin { Kind: BuiltinKind.Pointer })
            {
                var pType = _typeMap.Map(t, owned: false);
                return $"reinterpret_cast<{pType}>({ns}::readValue<std::uint64_t>({bufferVar}))";
            }

            if (t is IrType.Builtin { Kind: BuiltinKind.Function })
            {
                return $"{ns}::readFunction({bufferVar}, &{_runtime.DispatchQueueField})";
            }

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

            return $"{ns}::writeValue({bufferVar}, {valueExpr})";
        }

        /// <summary>
        /// Generates a read expression for a non-nullable, non-array type from the buffer.
        /// </summary>
        public override string ReadExpr(IrType t, string bufferVar)
        {
            if (t is IrType.Nullable or IrType.Array)
                throw new NotSupportedException("ReadExpr expects a non-nullable, non-array IrType. Use DecodeLines.");
            return ReadNonWrapped(t, bufferVar, owned: true);
        }

        /// <summary>
        /// Generates a write expression for a non-nullable, non-array type into the buffer.
        /// </summary>
        public override string WriteExpr(IrType t, string bufferVar, string valueExpr)
        {
            if (t is IrType.Nullable or IrType.Array)
                throw new NotSupportedException("WriteExpr expects a non-nullable, non-array IrType. Use EncodeLines.");
            return WriteNonWrapped(t, bufferVar, valueExpr);
        }

        /// <summary>
        /// Emits C++ code to decode a value from the buffer.
        /// </summary>
        public override void DecodeLines(TWriter w, IrType t, string accessor, bool declare, string bufferVar)
            => DecodeLines(w, t, accessor, declare, bufferVar, owned: true);

        /// <summary>
        /// Emits C++ code to encode a value into the buffer.
        /// </summary>
        public override void EncodeLines(TWriter w, IrType t, string accessor, string bufferVar)
        {
            var ns = _runtime.CodeGenNamespace;

            if (ContainsBuiltin(t, BuiltinKind.Buffer))
                throw new NotSupportedException("code emitter: buffers in return values are not supported.");

            if (ContainsBuiltin(t, BuiltinKind.Function))
                throw new NotSupportedException("code emitter: functions in return values are not supported.");

            w.Call($"{ns}::writeValue", bufferVar, accessor).Line(";");
        }

        /// <summary>
        /// Emits C++ code to decode a value from the buffer with ownership control.
        /// </summary>
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

            if (t is IrType.Nullable n)
            {
                var inner = n.Underlying;

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

            if (t is IrType.Builtin { Kind: BuiltinKind.Buffer })
            {
                w.Assign(accessor, ReadNonWrapped(t, bufferVar, owned), declType);
                w.Line($"{_runtime.BufferQueueField}.pop();");
                return;
            }

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
