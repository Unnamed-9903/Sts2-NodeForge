using System;

namespace NodeForge
{
    /// <summary>Localized text helper. Chinese is used for zhs/zht/zh, and also as the default when the
    /// localization manager is not ready yet (this is a Chinese-first mod UI).</summary>
    internal static class UiText
    {
        public static string T(string zh, string en)
        {
            try
            {
                var lm = MegaCrit.Sts2.Core.Localization.LocManager.Instance;
                var lang = lm?.Language;
                if (string.IsNullOrEmpty(lang)) return zh;               // not initialized yet -> Chinese
                if (lang!.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return zh;
                if (string.Equals(lang, "zhs", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(lang, "zht", StringComparison.OrdinalIgnoreCase)) return zh;
                return en;
            }
            catch { return zh; }
        }
    }

    internal static class Log
    {
        // Use the game's logger so messages appear in godot*.log (Console.WriteLine does not).
        public static void Info(string msg)
        {
            try { MegaCrit.Sts2.Core.Logging.Log.Info("[NodeForge] " + msg); } catch { }
        }
        public static void Warn(string msg)
        {
            try { MegaCrit.Sts2.Core.Logging.Log.Warn("[NodeForge] " + msg); } catch { }
        }
        public static void Error(string msg)
        {
            try { MegaCrit.Sts2.Core.Logging.Log.Error("[NodeForge] " + msg); } catch { }
        }
    }
}
