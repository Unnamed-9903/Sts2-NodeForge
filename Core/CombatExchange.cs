using System;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge
{
    /// <summary>
    /// "Equivalent Exchange / 万象之源" point pool. HOST-AUTHORITATIVE: a single pool lives on the host.
    /// Sacrificing a relic grants points (host pool); changing a node costs points. Only the host may
    /// change nodes. Multiplier applies ONLY to starting balance and node cost (NOT sacrifice gain).
    /// </summary>
    internal static class CombatExchange
    {
        public static double Points { get; private set; }

        /// <summary>Points shown in the UI. On the host this equals the authoritative pool; on clients it is
        /// the value last synced from the host (read-only display).</summary>
        public static double DisplayPoints { get { return IsAuthority ? Points : _clientView; } }
        private static double _clientView;

        /// <summary>Client: update the display-only balance from a host sync message.</summary>
        public static void SetClientView(double v)
        {
            _clientView = v;
            try { NodeForge.Menu2Overlay.NotifyPoolChanged(); } catch { }
        }

        public static bool Active
        {
            get { try { return Settings.Current.ExchangeEnabled; } catch { return false; } }
        }

        /// <summary>EMC settings may only be changed when no run is in progress (menu / not in gameplay).
        /// In-run they are locked. (Unfinished-save presence alone does NOT lock, so the menu still works.)</summary>
        public static bool SettingsEditable
        {
            get
            {
                try
                {
                    if (RunManager.Instance?.DebugOnlyGetState() != null) return false; // in a run
                    return true;
                }
                catch { return true; }
            }
        }

        /// <summary>Whether the local player is the authority that owns the pool (host or singleplayer).</summary>
        public static bool IsAuthority
        {
            get
            {
                try
                {
                    var t = RunManager.Instance?.NetService?.Type;
                    return t == null
                        || t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host
                        || t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Singleplayer
                        || t == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.None;
                }
                catch { return true; }
            }
        }

        public static int Multiplier()
        {
            var s = Settings.Current;
            // Singleplayer uses the SOLO multiplier; multiplayer uses the MULTIPLAYER multiplier
            // (fixed value or player-count).
            if (!NetAuth.IsMultiplayer())
                return Math.Max(1, (int)Math.Round(s.ExchangeMultiplierSolo));

            if (s.ExchangeMultiplierMode == NodeForgeSettings.ExchangeMultMode.PlayerCount)
            {
                try
                {
                    int n = RunManager.Instance?.DebugOnlyGetState()?.Players?.Count ?? 1;
                    return Math.Max(1, n);
                }
                catch { return 1; }
            }
            return Math.Max(1, (int)Math.Round(s.ExchangeMultiplier));
        }

        /// <summary>Reset the pool for a new run. Host-authoritative. Balance = start * multiplier.</summary>
        public static void ResetForRun()
        {
            if (!IsAuthority) return;
            var s = Settings.Current;
            double start = Math.Max(0, s.ExchangeStartPoints) * Multiplier();
            Points = start;
            Log.Info($"exchange: pool reset to {Points} (start {s.ExchangeStartPoints} x{Multiplier()})");
            try { NodeForgeNet.BroadcastPool(); } catch { }
            try { NodeForge.Menu2Overlay.NotifyPoolChanged(); } catch { }
        }

        /// <summary>Add points (host pool). Used by local sacrifice and by client-sacrifice messages.</summary>
        public static void AddPoints(double amount)
        {
            if (!IsAuthority) return;
            if (amount <= 0) return;
            Points += amount;
            Log.Info($"exchange: +{amount} -> {Points}");
            try { NodeForgeNet.BroadcastPool(); } catch { }
            try { NodeForge.Menu2Overlay.NotifyPoolChanged(); } catch { }
        }

        /// <summary>Try to spend points (host only). Returns false if not enough.</summary>
        public static bool TrySpend(double amount)
        {
            if (!IsAuthority) return false;
            if (amount <= 0) return true;
            if (Points + 1e-9 < amount) return false;
            Points -= amount;
            Log.Info($"exchange: -{amount} -> {Points}");
            try { NodeForgeNet.BroadcastPool(); } catch { }
            try { NodeForge.Menu2Overlay.NotifyPoolChanged(); } catch { }
            return true;
        }

        // ---------------- Relic sacrifice value ----------------
        public static double ValueOf(RelicRarity? r)
        {
            var s = Settings.Current;
            return r switch
            {
                RelicRarity.Starter => s.ExchangeValStarter,
                RelicRarity.Common => s.ExchangeValCommon,
                RelicRarity.Uncommon => s.ExchangeValUncommon,
                RelicRarity.Rare => s.ExchangeValRare,
                RelicRarity.Shop => s.ExchangeValShop,
                RelicRarity.Ancient => s.ExchangeValAncient,
                RelicRarity.Event => s.ExchangeValEvent,
                _ => s.ExchangeValCommon,
            };
        }

        // ---------------- Node-change cost ----------------
        /// <summary>Cost to change a node to the given target, based on the target config id.</summary>
        public static double CostForTarget(string cfgId)
        {
            var s = Settings.Current;
            switch ((cfgId ?? "").ToLowerInvariant())
            {
                case "monster": return s.ExchangeCostMonster * Multiplier();
                case "elite": return s.ExchangeCostElite * Multiplier();
                case "shop": return s.ExchangeCostShop * Multiplier();
                case "treasure": return s.ExchangeCostTreasure * Multiplier();
                case "rest_site": return s.ExchangeCostRest * Multiplier();
                case "event": return s.ExchangeCostEvent * Multiplier();
                case "ancient_event": return s.ExchangeCostAncient * Multiplier();
                case "question": return s.ExchangeCostQuestion * Multiplier();
                default: return s.ExchangeCostMonster * Multiplier();
            }
        }
    }
}


