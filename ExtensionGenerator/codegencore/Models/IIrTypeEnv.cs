namespace codegencore.Models
{
    public interface IIrTypeEnv
    {
        bool TryResolveNamed(string name, out IrNamedTypeInfo namedType);
    }
}