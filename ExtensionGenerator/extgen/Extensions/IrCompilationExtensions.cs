using codegencore.Models;
using extgen.Models;

namespace extgen.Extensions
{
    public static class IrCompilationExtensions
    {
        public static bool HasFunctionType(this IrCompilation c)
        {
            // 1. Flatten all fields (from all structs)
            var allFields = c.Structs.SelectMany(s => s.Fields);

            // 2. Flatten all functions (from the class AND all structs)
            var allFunctions = c.GetAllFunctions((s, f) => f);

            return allFields.Any(f => f.Type.ContainsBuiltin(BuiltinKind.Function)) || allFunctions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Function)));
        }

        public static bool HasBufferType(this IrCompilation c)
        {
            // 1. Flatten all fields (from all structs)
            var allFields = c.Structs.SelectMany(s => s.Fields);

            // 2. Flatten all functions (from the class AND all structs)
            var allFunctions = c.GetAllFunctions((s, f) => f);

            return allFields.Any(f => f.Type.ContainsBuiltin(BuiltinKind.Buffer)) || allFunctions.Any(f => f.Parameters.Any(p => p.Type.ContainsBuiltin(BuiltinKind.Buffer)));
        }

        public static bool HasOptionalTypes(this IrCompilation c) 
        {
            // 1. Flatten all fields (from all structs)
            var allFields = c.Structs.SelectMany(s => s.Fields);

            // 2. Flatten all functions (from the class AND all structs)
            var allFunctions = c.GetAllFunctions((s, f) => f);

            return allFields.Any(f => f.Type.IsNullable()) || allFunctions.Any(f => f.Parameters.Any(p => p.Type.IsNullable()) || f.ReturnType.IsNullable());
        }

        public static bool HasListTypes(this IrCompilation c) {

            // 1. Flatten all fields (from all structs)
            var allFields = c.Structs.SelectMany(s => s.Fields);

            // 2. Flatten all functions (from the class AND all structs)
            var allFunctions = c.GetAllFunctions((s, f) => f);

            return allFields.Any(f => f.Type.IsVarArray()) || allFunctions.Any(f => f.Parameters.Any(p => p.Type.IsVarArray()) || f.ReturnType.IsVarArray());
        }

        public static bool HasArrayTypes(this IrCompilation c) 
        {
            // 1. Flatten all fields (from all structs)
            var allFields = c.Structs.SelectMany(s => s.Fields);

            // 2. Flatten all functions (from the class AND all structs)
            var allFunctions = c.GetAllFunctions((s, f) => f);

            return allFields.Any(f => f.Type.IsFixedArray()) || allFunctions.Any(f => f.Parameters.Any(p => p.Type.IsFixedArray()) || f.ReturnType.IsFixedArray()); ;
        }
    }
}
