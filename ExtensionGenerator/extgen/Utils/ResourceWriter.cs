using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace extgen.Utils
{
    /// <summary>
    /// Helpers for writing embedded resources to disk.
    /// </summary>
    internal static class ResourceWriter
    {
        /// <summary>
        /// Writes an embedded resource to disk.
        /// Overwrites only if contents have changed.
        /// </summary>
        /// <param name="assembly">Assembly containing the embedded resource.</param>
        /// <param name="resourceName">Fully qualified resource name.</param>
        /// <param name="destinationPath">Destination file path.</param>
        /// <param name="encoding">Text encoding (defaults to UTF-8 without BOM).</param>
        public static void WriteTextResource(
            Assembly assembly,
            string resourceName,
            string destinationPath,
            Encoding? encoding = null)
        {
            encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. " +
                    $"Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
            var newText = reader.ReadToEnd();

            newText = newText.Replace("\r\n", "\n");

            if (File.Exists(destinationPath))
            {
                var oldText = File.ReadAllText(destinationPath, encoding);
                if (string.Equals(oldText, newText, StringComparison.Ordinal))
                    return;
            }

            File.WriteAllText(destinationPath, newText, encoding);
        }

        /// <summary>
        /// Writes an embedded resource with template token substitution.
        /// </summary>
        /// <param name="assembly">Assembly containing the embedded resource.</param>
        /// <param name="resourceName">Fully qualified resource name.</param>
        /// <param name="destinationPath">Destination file path.</param>
        /// <param name="tokens">Token substitution dictionary (key -> value).</param>
        /// <param name="encoding">Text encoding (defaults to UTF-8 without BOM).</param>
        public static void WriteTemplatedTextResource(
            Assembly assembly,
            string resourceName,
            string destinationPath,
            IReadOnlyDictionary<string, string> tokens,
            Encoding? encoding = null)
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
