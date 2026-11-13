using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace NodeForge.Patches
{
    /// <summary>Global hotkeys to toggle the two overlays. Supports modifier combos like "Ctrl+F6".</summary>
    [HarmonyPatch(typeof(NGame), "_Input")]
    internal static class NGameInputPatch
    {
        private static void Postfix(NGame __instance, InputEvent inputEvent)
        {
            if (inputEvent is not InputEventKey { Pressed: true, Echo: false } k) return;

            try
            {
                var cfg = Settings.Current;

                if (MatchesCombo(k, cfg.Menu1Hotkey))
                {
                    Menu1Overlay.Toggle(__instance);
                    __instance.GetViewport().SetInputAsHandled();
                }
                else if (MatchesCombo(k, cfg.Menu2Hotkey))
                {
                    Menu2Overlay.Toggle(__instance);
                    __instance.GetViewport().SetInputAsHandled();
                }
                else if ((long)k.Keycode == (long)Key.Escape)
                {
                    bool handled = Menu1Overlay.CloseIfOpen() | Menu2Overlay.CloseIfOpen();
                    if (handled) __instance.GetViewport().SetInputAsHandled();
                }
            }
            catch (Exception e) { Log.Warn("input patch: " + e.Message); }
        }

        /// <summary>Parse combos like "F6", "Ctrl+F6", "Shift+Ctrl+F6", "Key.F6".</summary>
        private static bool MatchesCombo(InputEventKey k, string? combo)
        {
            if (string.IsNullOrWhiteSpace(combo)) return false;
            bool needCtrl = false, needShift = false, needAlt = false;
            string keyPart = combo!.Trim();

            var parts = keyPart.Split('+');
            string keyName = parts[parts.Length - 1].Trim();
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var m = parts[i].Trim().ToLowerInvariant();
                if (m is "ctrl" or "control") needCtrl = true;
                else if (m is "shift") needShift = true;
                else if (m is "alt") needAlt = true;
            }

            if (keyName.StartsWith("Key.", StringComparison.OrdinalIgnoreCase)) keyName = keyName.Substring(4);
            if (!Enum.TryParse<Key>(keyName, true, out var key)) return false;

            bool keyOk = (long)k.Keycode == (long)key || (long)k.PhysicalKeycode == (long)key;
            if (!keyOk) return false;

            // Require modifier state to match exactly what the combo asked for.
            bool ctrl = k.CtrlPressed, shift = k.ShiftPressed, alt = k.AltPressed;
            return ctrl == needCtrl && shift == needShift && alt == needAlt;
        }
    }
}
