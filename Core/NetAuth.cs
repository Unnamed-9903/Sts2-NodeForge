using MegaCrit.Sts2.Core.Runs;

namespace NodeForge
{
    /// <summary>Multiplayer authority check: only the host (or singleplayer) may edit.</summary>
    internal static class NetAuth
    {
        /// <summary>True when the local player is allowed to modify the map (singleplayer or host).</summary>
        public static bool CanEdit()
        {
            try
            {
                var rm = RunManager.Instance;
                var net = rm?.NetService;
                if (net == null) return true; // not in a run/offline -> allow (menus)
                var t = net.Type;
                // Singleplayer and Host may edit; Client/Replay/None must not.
                return t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Singleplayer
                    || t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host
                    || t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.None;
            }
            catch { return true; }
        }

        /// <summary>True when we're a non-host client (edits would not take effect).</summary>
        public static bool IsClient()
        {
            try
            {
                var t = RunManager.Instance?.NetService?.Type;
                return t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Client;
            }
            catch { return false; }
        }

        /// <summary>True when in an actual multiplayer session (Host or Client), not singleplayer/offline.</summary>
        public static bool IsMultiplayer()
        {
            try
            {
                var t = RunManager.Instance?.NetService?.Type;
                return t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host
                    || t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Client;
            }
            catch { return false; }
        }

        /// <summary>True when a run is actually in progress (map exists). Bulk edits are only allowed then —
        /// not in the lobby/character-select phase. Works in singleplayer AND multiplayer (host).</summary>
        public static bool InRun()
        {
            try { return RunManager.Instance?.DebugOnlyGetState() != null; }
            catch { return false; }
        }

        /// <summary>Bulk (mass) edits are allowed once a run is in progress (singleplayer or multiplayer host).
        /// Not allowed before the run starts (lobby / character select).</summary>
        public static bool BatchAllowed() => InRun() && CanEdit();
    }
}
