using extgen.Models;
using extgen.Parsing.Validation;
using GMIDL_FB;
using gmidlparser;
using gmidlreader;
using Google.FlatBuffers;

namespace extgen.Parsing.Gmidl
{
    /// <summary>
    /// Loads and validates GMIDL schema files into IR compilation units.
    /// GMIDL = GameMaker Interface Definition Language (custom schema format for defining extension APIs).
    /// </summary>
    internal class GmidlSchemaLoader
    {
        /// <summary>
        /// Loads a GMIDL schema from a file, parses it, and validates the resulting IR.
        /// </summary>
        /// <param name="path">Path to the GMIDL schema file (.gmidl extension).</param>
        /// <returns>Validated IR compilation.</returns>
        public static IrCompilation LoadFromFile(string path)
        {
            // GMIDL files use FlatBuffers for serialization (Google's cross-platform binary format).
            // The GMIDLCompiler.Parse() reads the .gmidl source, compiles it to FlatBuffer bytes,
            // then we deserialize those bytes into a GMIDLDatabase object for processing.
            // This two-step process (text→flatbuffer→database) separates parsing from IR construction.

            GMIDLDatabase? db = null;
            try
            {
                // Parse GMIDL source text → FlatBuffer bytes
                byte[] bytes = GMIDLCompiler.Parse(path);

                // Deserialize FlatBuffer bytes → structured document
                var bb = new ByteBuffer(bytes);
                var document = GMIDL_FB_Document.GetRootAsGMIDL_FB_Document(bb);

                // Load document into database (normalizes FlatBuffer schema into queryable form)
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
                new FunctionCommonPrefixRule(),

                // Modifiers
                new StructMethodsCannotHaveModifiers(),
                new FunctionModifiersMustBeUnique()
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
