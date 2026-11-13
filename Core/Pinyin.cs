using System;
using System.Text;

namespace NodeForge
{
    /// <summary>
    /// Pinyin search for CJK using an auto-generated full-pinyin table (from pypinyin, no tone).
    /// Supports full-pinyin matching (luo/wa) and initials matching (lnwp), per PinyinMode.
    /// </summary>
    internal static class Pinyin
    {
        public static bool Available => PinyinData.Map.Count > 0;

        /// <summary>Concatenated full pinyin (non-Han kept lowercased).</summary>
        public static string PinyinOf(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s!.Length * 2);
            foreach (var ch in s)
            {
                if (ch >= 0x4E00 && ch <= 0x9FA5 && PinyinData.Map.TryGetValue(ch, out var py))
                    sb.Append(py);
                else if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        /// <summary>Initials only (first letter of each Han char's pinyin).</summary>
        public static string InitialsOf(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s!.Length);
            foreach (var ch in s)
            {
                if (ch >= 0x4E00 && ch <= 0x9FA5 && PinyinData.Map.TryGetValue(ch, out var py) && py.Length > 0)
                    sb.Append(py[0]);
                else if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        public static bool Matches(string? title, string? id, string query, NodeForgeSettings.PinyinSearchMode mode)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var q = query.Trim();
            if ((title?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0) return true;
            if ((id?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0) return true;

            if (!Available) return false;
            var ql = q.Replace(" ", "").ToLowerInvariant();
            foreach (var c in ql) if (c < 'a' || c > 'z') return false;

            bool full = mode == NodeForgeSettings.PinyinSearchMode.Full || mode == NodeForgeSettings.PinyinSearchMode.Both;
            bool init = mode == NodeForgeSettings.PinyinSearchMode.Initial || mode == NodeForgeSettings.PinyinSearchMode.Both;

            if (full)
            {
                if (PinyinOf(title).Contains(ql, StringComparison.Ordinal)) return true;
                if (PinyinOf(id).Contains(ql, StringComparison.Ordinal)) return true;
            }
            if (init)
            {
                if (InitialsOf(title).Contains(ql, StringComparison.Ordinal)) return true;
                if (InitialsOf(id).Contains(ql, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}

