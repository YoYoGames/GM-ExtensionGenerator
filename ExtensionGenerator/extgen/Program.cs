using extgen.App;
using extgen.Config;
using NDesk.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace extgen
{
    public static class Program
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        public static int Main(string[] args)
        {
            string? configPath = null;
            string? initDir = null;
            bool showHelp = false;

            var options = new OptionSet {
                { "c|config=", "Path to JSON config file.", v => configPath = v },
                { "i|init=", "Initialize a new config + schema in the given folder.", v => initDir = v },
                { "h|help",    "Show help.", v => showHelp = v != null }
            };

            try
            {
                Console.WriteLine(BuildInfo.FullVersion);

                var extras = options.Parse(args);

                if (!string.IsNullOrWhiteSpace(initDir))
                {
                    var schemaSvc = new ConfigSchemaService(JsonOptions);
                    var initializer = new ProjectInitializer(schemaSvc, JsonOptions);
                    return initializer.Init(initDir);
                }

                if (showHelp || string.IsNullOrWhiteSpace(configPath) || extras.Count > 0)
                {
                    ShowUsage(options);
                    return showHelp ? 0 : 1;
                }

                var schemaService = new ConfigSchemaService(JsonOptions);
                var runner = new CodegenRunner(JsonOptions, schemaService);
                return runner.RunFromConfig(configPath!);
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine(e.Message);
                ShowUsage(options);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 99;
            }
        }

        private static void ShowUsage(OptionSet options)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  extgen --config <path/to/config.json>");
            Console.WriteLine("  extgen --init <folder>");
            Console.WriteLine();
            options.WriteOptionDescriptions(Console.Out);
        }
    }
}
