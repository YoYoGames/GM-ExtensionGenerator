using extgen.Models;
using extgen.Parsing.Validation;
using GMIDL_FB;
using gmidlparser;
using gmidlreader;
using Google.FlatBuffers;

namespace extgen.Parsing.Gmidl
{
    internal class GmidlSchemaLoader
    {
        public static IrCompilation LoadFromFile(string path)
        {
            GMIDLDatabase? db = null;
            try
            {
                byte[] bytes = GMIDLCompiler.Parse(path);

                var bb = new ByteBuffer(bytes);
                var document = GMIDL_FB_Document.GetRootAsGMIDL_FB_Document(bb);

                db = new GMIDLDatabase();
                db.AddFlatBuffer(document);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
                Environment.Exit(1);
            }

            var dto = db ?? throw new InvalidOperationException("Invalid GMIDL schema");

            var comp = new GmidlSchemaParser(dto).Build();

            var validator = new IrValidator(
                // Types
                new NoDuplicateSymbolsRule(),
                new NoUnknownTypeAllowedRule(),

                // Buffer and Function
                new NoBufferOrFunctionInStructFieldsRule(),
                new NoBufferOrFunctionReturnTypesRule(),
                
                // Enum
                new EnumUnderlyingMustBeIntegralScalarRule(),
                new EnumMemberNamesMustBeUniqueRule(),

                // Naming
                new NoUnderscoresInCompilationNameRule(),
                new FunctionCommonPrefixRule()
            );

            var diags = validator.Validate(comp);
            var errors = diags.Where(d => d.Severity == IrSeverity.Error).ToArray();

            if (diags.Length > 0)
            {
                foreach (var d in diags.OrderByDescending(d => d.Severity))
                    Console.Error.WriteLine($"{d.Severity} {d.Code}: {d.Message}" +
                                            (d.Path is null ? "" : $" @ {d.Path}"));

                if (errors.Length > 0)
                    throw new InvalidOperationException($"IR validation failed with {errors.Length} error(s).");
            }

            return comp;
        }
    }
}
