using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using NodeForge.UI;

namespace NodeForge
{
    /// <summary>Global lightweight toast shown to BOTH host and clients when a node edit is synced.</summary>
    internal static class SyncToast
    {
        private static Label? _label;
        private static SceneTreeTimer? _timer;

        public static void Show(string msg)
        {
            try
            {
                var game = NGame.Instance;
                if (game == null) return;
                if (_label == null || !GodotObject.IsInstanceValid(_label))
                {
                    _label = new Label
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                        ZIndex = 2000,
                    };
                    _label.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
                    _label.Position = new Vector2(-360f, 90f);
                    _label.CustomMinimumSize = new Vector2(720f, 0f);
                    _label.AddThemeFontSizeOverride("font_size", 18);
                    _label.AddThemeColorOverride("font_color", new Color("7FD8FF"));
                    // Own high CanvasLayer so it draws above the game UI.
                    var layer = new CanvasLayer { Layer = 250, Name = "NodeForgeSyncToast" };
                    layer.AddChild(_label);
                    game.AddChild(layer);
                }
                _label.Text = msg;
                _label.Visible = true;
                try { _timer?.Disconnect(SceneTreeTimer.SignalName.Timeout, Callable.From(OnTimeout)); } catch { }
                _timer = game.GetTree().CreateTimer(5.0, true, false, false);
                _timer.Timeout += OnTimeout;
            }
            catch (Exception e) { Log.Warn("toast: " + e.Message); }
        }

        private static void OnTimeout()
        {
            try { if (_label != null && GodotObject.IsInstanceValid(_label)) _label.Visible = false; } catch { }
        }
    }
}
