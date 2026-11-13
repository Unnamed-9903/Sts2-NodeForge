using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Helpers;
using Godot;

namespace NodeForge
{
    /// <summary>
    /// Converts STS2's custom rich-text tags (jitter/sine/purple/...) into standard BBCode that a
    /// plain RichTextLabel understands, so event text renders correctly (colors kept, effects stripped).
    /// </summary>
    internal static class MegaText
    {
        private static readonly Dictionary<string, Color> Colors = new(StringComparer.OrdinalIgnoreCase)
        {
            ["red"] = StsColors.red,
            ["green"] = StsColors.green,
            ["blue"] = StsColors.blue,
            ["yellow"] = StsColors.yellow,
            ["purple"] = StsColors.purple,
            ["orange"] = StsColors.orange,
            ["pink"] = StsColors.pink,
            ["gold"] = StsColors.gold,
            ["aqua"] = StsColors.aqua,
        };

        // Matches [tag] or [tag=...] or [/tag]
        private static readonly Regex TagRx = new(@"\[(/?)([A-Za-z_][A-Za-z0-9_]*)(=[^\]]*)?\]", RegexOptions.Compiled);

        public static string ToBbcode(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return TagRx.Replace(raw!, m =>
            {
                string closing = m.Groups[1].Value;
                string name = m.Groups[2].Value;
                string arg = m.Groups[3].Value;
                if (Colors.TryGetValue(name, out var c))
                {
                    return closing == "/" ? "[/color]" : $"[color=#{c.ToHtml()}]";
                }
                // Effect / unknown tags (jitter, sine, fly_in, fade_in, scramble, thinky_dots,
                // ancient_banner, b, i, color, bgcolor, ...): drop the custom wrapper, keep inner text.
                // Known standard BBCode we can pass through unchanged:
                switch (name.ToLowerInvariant())
                {
                    case "b": case "i": case "u": case "s":
                    case "color": case "bgcolor": case "font": case "font_size":
                    case "center": case "left": case "right": case "url":
                        return m.Value;
                    default:
                        return ""; // strip custom/effect tag
                }
            });
        }
    }
}
