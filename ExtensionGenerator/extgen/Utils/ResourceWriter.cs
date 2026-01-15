using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace extgen.Utils
{
    internal static class ResourceWriter
    {
        /// <summary>Writes an embedded resource to disk. Overwrites only if contents changed.</summary>
        public static void WriteTextResource(
            Assembly assembly,
            string resourceName,
            string destinationPath,
            Encoding? encoding = null)
        {
            encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // UTF-8 no BOM
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. " +
                    $"Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
            var newText = reader.ReadToEnd();

            // Optional: normalize line endings to LF for C/C++ toolchains
            newText = newText.Replace("\r\n", "\n");

            if (File.Exists(destinationPath))
            {
                var oldText = File.ReadAllText(destinationPath, encoding);
                if (string.Equals(oldText, newText, StringComparison.Ordinal))
                    return; // no write needed
            }

            File.WriteAllText(destinationPath, newText, encoding);
        }

        public static void WriteTemplatedTextResource(
            Assembly assembly, string resourceName, string destinationPath,
            IReadOnlyDictionary<string, string> tokens, Encoding? encoding = null)
        {
            encoding ??= new UTF8Encoding(false);
            using var s = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing resource {resourceName}");
            using var r = new StreamReader(s, Encoding.UTF8);
            var text = r.ReadToEnd();

            foreach (var (k, v) in tokens)
                text = text.Replace("${" + k + "}", v);

            text = text.Replace("\r\n", "\n");
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllText(destinationPath, text, encoding);
        }
    }
}
