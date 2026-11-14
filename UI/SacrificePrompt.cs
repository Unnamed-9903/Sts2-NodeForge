using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using NodeForge.UI;

namespace NodeForge
{
    /// <summary>Small modal asking the player to confirm sacrificing a relic, showing the points it is worth.</summary>
    internal sealed class SacrificePrompt : CanvasLayer
    {
        private static SacrificePrompt? _inst;

        public static void Show(RelicModel relic, string relicName, double value)
        {
            try
            {
                var game = NGame.Instance;
                if (game == null) return;
                if (_inst != null && GodotObject.IsInstanceValid(_inst)) _inst.QueueFree();
                _inst = new SacrificePrompt { Name = "NodeForgeSacrificePrompt" };
                _inst.Layer = 240;
                game.AddChild(_inst);
                _inst.Build(relic, relicName, value);
            }
            catch (Exception e) { Log.Warn("sacrifice prompt: " + e.Message); }
        }

        private void Build(RelicModel relic, string relicName, double value)
        {
            var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
            backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            backdrop.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) Close(); };
            AddChild(backdrop);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            center.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(center);

            var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(420, 0) };
            var sb = new StyleBoxFlat
            {
                BgColor = new Color("10242A"),
                BorderColor = UiTheme.Accent,
                BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2,
                CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
                ContentMarginLeft = 18f, ContentMarginRight = 18f, ContentMarginTop = 16f, ContentMarginBottom = 16f,
            };
            panel.AddThemeStyleboxOverride("panel", sb);
            center.AddChild(panel);

            var v = new VBoxContainer();
            panel.AddChild(v);

            var title = new Label { Text = UiText.T("献祭遗物", "Sacrifice Relic"), HorizontalAlignment = HorizontalAlignment.Center };
            title.AddThemeFontSizeOverride("font_size", 20);
            title.AddThemeColorOverride("font_color", UiTheme.Accent);
            v.AddChild(title);

            var msg = new Label
            {
                Text = UiText.T(
                    $"是否献祭「{relicName}」？\n可获得 {value:0} 点万象之源，遗物将被永久移除。",
                    $"Sacrifice \"{relicName}\"?\nGain {value:0} EMC point(s); the relic will be permanently removed."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            msg.AddThemeFontSizeOverride("font_size", 15);
            v.AddChild(msg);

            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 16);
            v.AddChild(row);

            var yes = UiTheme.MakeButton(UiText.T("是，献祭", "Yes, sacrifice"), 15);
            yes.CustomMinimumSize = new Vector2(160f, 40f);
            yes.Pressed += () => { RelicSacrifice.Commit(relic, value); Close(); };
            row.AddChild(yes);

            var no = UiTheme.MakeButton(UiText.T("取消", "Cancel"), 15);
            no.CustomMinimumSize = new Vector2(120f, 40f);
            no.Pressed += Close;
            row.AddChild(no);
        }

        private void Close()
        {
            try { QueueFree(); } catch { }
            _inst = null;
        }
    }
}
