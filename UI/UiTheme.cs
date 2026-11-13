using System;
using Godot;

namespace NodeForge.UI
{
    /// <summary>Visual theme distinct from the original nodeswap look (dark teal + amber accents).</summary>
    internal static class UiTheme
    {
        public static readonly Color Bg = new("0E1B1E");
        public static readonly Color BgPanel = new("13282C");
        public static readonly Color Accent = new("E8A54B");       // amber
        public static readonly Color AccentDim = new("B37F38");
        public static readonly Color Text = new("E7F0EE");
        public static readonly Color TextDim = new("9FB4B0");
        public static readonly Color Ok = new("6FD3A8");
        public static readonly Color Warn = new("E8795E");
        public static readonly Color ItemBg = new("1B3338");
        public static readonly Color ItemHover = new("245057");

        public static StyleBoxFlat Panel()
        {
            var s = new StyleBoxFlat
            {
                BgColor = BgPanel,
                BorderColor = Accent,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                ContentMarginBottom = 16f,
                ContentMarginLeft = 16f,
                ContentMarginRight = 16f,
                ContentMarginTop = 16f,
            };
            return s;
        }

        public static StyleBoxFlat Item(bool hover = false)
        {
            return new StyleBoxFlat
            {
                BgColor = hover ? ItemHover : ItemBg,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                ContentMarginLeft = 8f,
                ContentMarginRight = 8f,
                ContentMarginTop = 4f,
                ContentMarginBottom = 4f,
            };
        }

        public static void StyleLabel(Label l, int size, Color color)
        {
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", color);
        }

        /// <summary>Parse the color string exactly as RitsuLib stores it (hex / Godot HTML / 4-component),
        /// then fall back to Godot's own parser. This keeps our colours in sync with the settings picker.</summary>
        public static Color ParseColor(string? text, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            var h = text!.Trim();
            // 1) RitsuLib's own deserializer (handles BaseLib 4-component + hex + HTML).
            try
            {
                if (STS2RitsuLib.Settings.ModSettingsColorControl.TryDeserializeColorForSettings(h, out var c))
                    return c;
            }
            catch { }
            // 2) Godot string parser (handles "RRGGBB" / "RRGGBBAA" / "#..." / named colours).
            try
            {
                var s2 = h;
                return new Color(s2);
            }
            catch { }
            return fallback;
        }

        public static Color EventColor(EventColorClass cls)
        {
            var s = Settings.Current;
            return cls switch
            {
                EventColorClass.Red => ParseColor(s.EventColorRed, Warn),
                EventColorClass.Orange => ParseColor(s.EventColorOrange, new Color("F08A3C")),
                EventColorClass.Yellow => ParseColor(s.EventColorYellow, Accent),
                EventColorClass.Missing => ParseColor(s.EventColorVisited, new Color("555555")),
                _ => ParseColor(s.EventColorGreen, Ok),
            };
        }

        public static Color Highlight(Color c) => c.Lerp(Colors.White, 0.35f);

        /// <summary>Reliably center a fixed-size Control in the viewport.</summary>
        public static void CenterPanel(Control panel, float w, float h)
        {
            panel.CustomMinimumSize = new Vector2(w, h);
            panel.Size = new Vector2(w, h);
            panel.SetAnchorsPreset(Control.LayoutPreset.Center);
            panel.Position = new Vector2(-w / 2f, -h / 2f);
        }

        /// <summary>Center a fixed-size panel, clamped to 90% of the current viewport.</summary>
        public static void CenterPanelFit(Control panel, float wantW, float wantH, Vector2 viewport)
        {
            float w = Mathf.Min(wantW, viewport.X * 0.92f);
            float h = Mathf.Min(wantH, viewport.Y * 0.92f);
            CenterPanel(panel, w, h);
        }

        public static Button MakeButton(string text, int fontSize = 18)
        {
            var b = new Button { Text = text, CustomMinimumSize = new Vector2(110f, 40f) };
            b.AddThemeFontSizeOverride("font_size", fontSize);
            b.AddThemeColorOverride("font_color", Text);
            var normal = new StyleBoxFlat
            {
                BgColor = ItemBg,
                BorderColor = AccentDim,
                BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2,
                CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            };
            b.AddThemeStyleboxOverride("normal", normal);
            b.AddThemeStyleboxOverride("hover", Item(true));
            b.AddThemeStyleboxOverride("pressed", Item(true));
            return b;
        }
    }
}
