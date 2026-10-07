using System;
using System.Globalization;
using System.Text;

namespace BombArena.Core
{
    /// <summary>
    /// Seals a saved profile so a damaged file is noticed instead of loading half a profile: the text starts with a
    /// format line and ends with a checksum of everything above it. Saves from before sealing (no format line) are
    /// still accepted.
    /// </summary>
    public static class ProfileFile
    {
        private const string FormatLine = "format = 2\n";
        private const string CheckKey = "check = ";

        public static string Seal(string body)
        {
            var text = FormatLine + body;
            return text + CheckKey + Crc32(text).ToString("x8", CultureInfo.InvariantCulture) + "\n";
        }

        /// <summary>The profile text inside a save, or false if the save is damaged (cut short, altered or empty).</summary>
        public static bool TryOpen(string text, out string body)
        {
            body = "";
            if (string.IsNullOrEmpty(text)) return false;
            text = text.Replace("\r\n", "\n");
            if (!text.StartsWith(FormatLine, StringComparison.Ordinal))
            {
                body = text; // saved before sealing
                return text.Trim().Length > 0;
            }
            int at = text.LastIndexOf(CheckKey, StringComparison.Ordinal);
            if (at < 0) return false;
            var expected = text.Substring(at + CheckKey.Length).Trim();
            var sealedPart = text.Substring(0, at);
            if (!string.Equals(expected, Crc32(sealedPart).ToString("x8", CultureInfo.InvariantCulture), StringComparison.Ordinal)) return false;
            body = sealedPart.Substring(FormatLine.Length);
            return true;
        }

        private static uint Crc32(string text)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                crc ^= b;
                for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
            }
            return ~crc;
        }
    }
}
