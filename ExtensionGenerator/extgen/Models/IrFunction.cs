using codegencore.Models;
using System.Collections.Immutable;

namespace extgen.Models
{
    public sealed record IrFunction(string Name, IrType ReturnType, ImmutableArray<IrParameter> Parameters, IrParameter? Self, bool Hidden = false) 
    {
        // Helper to get the full flattened list
        public ImmutableArray<IrParameter> FullParameters =>
            Self is null ? Parameters : [Self, .. Parameters];
    }

}
