using codegencore.Models;
using extgen.Models;

namespace extgen.Models.Utils
{
    /// <summary>
    /// Resolves extra semantic info not carried by IrType itself.
    /// In particular: enum underlying types (needed for wire encoding).
    /// </summary>
    public interface IIrTypeEnumResolver
    {
        public IrType GetUnderlying(string enumName);

        public bool TryGetUnderlying(string enumName, out IrType underlying);
    }

    public sealed class IrTypeEnumResolver(IEnumerable<IrEnum> enums) : IIrTypeEnumResolver
    {
        private readonly Dictionary<string, IrType> _map = enums.ToDictionary(e => e.Name, e => e.Underlying, StringComparer.Ordinal);

        public IrType GetUnderlying(string enumName)
        {
            if (!_map.TryGetValue(enumName, out IrType? value)) throw new InvalidOperationException($"Enum {enumName} missing underlying type.");

            return value;
        }

        public bool TryGetUnderlying(string enumName, out IrType underlying)
        {
            underlying = IrType.Any;
            if (!_map.TryGetValue(enumName, out IrType? value))
                return false;

            underlying = value ?? IrType.Any;
            return true;
        }
    }
}
