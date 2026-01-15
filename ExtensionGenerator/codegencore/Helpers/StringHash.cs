using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace extgencore.Helpers
{
    /// <summary>
    /// Non-cryptographic 32-bit hash helper (FNV-1a).
    /// Use when you need a deterministic int key for a string
    /// (e.g., switch tables, generated IDs, etc.).
    /// </summary>
    public static class StringHash
    {
        private const uint Offset = 2166136261;   // FNV offset basis
        private const uint Prime = 16777619;     // FNV prime

        /// <summary>
        ///   Hashes <paramref name="text"/> to a signed 32-bit int.
        ///   Result is stable across processes and platforms.
        /// </summary>
        public static uint ToUInt32(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            uint hash = Offset;
            foreach (var ch in text.AsSpan())
            {
                hash ^= ch;
                hash *= Prime;
            }

            // Cast unchecked to keep the original bit pattern
            return unchecked(hash);
        }
    }
}
