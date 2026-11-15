using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace NodeForge
{
    /// <summary>Handles relic sacrifice in Equivalent-Exchange mode: hover a relic in your own relic bar,
    /// press the sacrifice key (F6), confirm, and the relic is removed (official RelicCmd.Remove) for points.</summary>
    internal static class RelicSacrifice
    {
        /// <summary>Returns the relic under the mouse in the local player's relic bar, or null.</summary>
        public static RelicModel? FindHoveredRelic()
        {
            try
            {
                var inv = NRun.Instance?.GlobalUi?.RelicInventory;
                if (inv == null) return null;
                Vector2 mouse = inv.GetGlobalMousePosition();
                foreach (var holder in inv.RelicNodes)
                {
                    if (holder == null || !GodotObject.IsInstanceValid(holder)) continue;
                    if (!holder.Visible) continue;
                    var r = holder.GetGlobalRect();
                    if (r.HasPoint(mouse) && holder.Relic != null)
                        return holder.Relic.Model;
                }
            }
            catch (Exception e) { Log.Warn("find hovered relic: " + e.Message); }
            return null;
        }

        /// <summary>Called by the F6 key handler when exchange mode is on. Shows a confirm dialog.</summary>
        public static void BeginSacrifice(RelicModel relic)
        {
            try
            {
                if (relic == null) return;
                double val = CombatExchange.ValueOf(GetRarity(relic));
                string name = "";
                try { name = relic.Title?.GetRawText() ?? relic.Id?.Entry ?? ""; } catch { }
                SacrificePrompt.Show(relic, name, val);
            }
            catch (Exception e) { Log.Warn("begin sacrifice: " + e.Message); }
        }

        private static MegaCrit.Sts2.Core.Entities.Relics.RelicRarity? GetRarity(RelicModel relic)
        {
            try { return relic.Rarity; } catch { return null; }
        }

        /// <summary>Called by the confirm dialog. Host removes locally + broadcasts; client sends a request
        /// to the host (host-authoritative removal keeps all peers in sync).</summary>
        public static void Commit(RelicModel relic, double value)
        {
            try
            {
                if (relic == null) return;
                if (CombatExchange.IsAuthority)
                {
                    ulong netId = 0; try { netId = relic.Owner?.NetId ?? 0; } catch { }
                    string rid = ""; try { rid = relic.Id?.Entry ?? ""; } catch { }
                    _ = RemoveAsync(relic, () =>
                    {
                        CombatExchange.AddPoints(value);
                        NodeForgeNet.BroadcastSacrificeDone(netId, rid);
                    });
                }
                else
                {
                    // Client: don't remove locally; ask host to do it (host broadcasts removal to all).
                    ulong netId = 0; try { var nn = MegaCrit.Sts2.Core.Context.LocalContext.NetId; if (nn != null) netId = nn.Value; } catch { }
                    string rid = ""; try { rid = relic.Id?.Entry ?? ""; } catch { }
                    NodeForgeNet.SendSacrificeRequest(netId, rid, value);
                }
            }
            catch (Exception e) { Log.Warn("sacrifice commit: " + e.Message); }
        }

        private static async System.Threading.Tasks.Task RemoveAsync(RelicModel relic, Action? after)
        {
            try { await MegaCrit.Sts2.Core.Commands.RelicCmd.Remove(relic); after?.Invoke(); }
            catch (Exception e) { Log.Warn("relic remove: " + e.Message); }
        }

        /// <summary>Host-side: find the player's relic by id, remove it (official API), add points, broadcast.</summary>
        public static void HostPerformSacrifice(ulong playerNetId, string relicId, double clientPoints)
        {
            try
            {
                var st = MegaCrit.Sts2.Core.Runs.RunManager.Instance?.DebugOnlyGetState();
                var player = FindPlayer(st, playerNetId);
                if (player == null) { Log.Warn("sacrifice: player not found " + playerNetId); return; }
                var relic = FindRelic(player, relicId);
                if (relic == null) { Log.Warn("sacrifice: relic not found " + relicId); return; }
                // Host recomputes the value authoritatively from the relic's rarity.
                double pts = CombatExchange.ValueOf(GetRarity(relic));
                if (pts <= 0) pts = clientPoints;
                _ = RemoveAsync(relic, () =>
                {
                    CombatExchange.AddPoints(pts);
                    NodeForgeNet.BroadcastSacrificeDone(playerNetId, relicId);
                    Log.Info($"sacrifice (host): player {playerNetId} relic {relicId} +{pts}");
                });
            }
            catch (Exception e) { Log.Warn("host sacrifice: " + e.Message); }
        }

        /// <summary>Client-side: remove the relic the host told us was sacrificed (keep in sync, no points).</summary>
        public static void ClientRemoveSacrificed(ulong playerNetId, string relicId)
        {
            try
            {
                var st = MegaCrit.Sts2.Core.Runs.RunManager.Instance?.DebugOnlyGetState();
                var player = FindPlayer(st, playerNetId);
                if (player == null) return;
                var relic = FindRelic(player, relicId);
                if (relic == null) return;
                _ = RemoveAsync(relic, null);
            }
            catch (Exception e) { Log.Warn("client remove sacrificed: " + e.Message); }
        }

        private static MegaCrit.Sts2.Core.Entities.Players.Player? FindPlayer(MegaCrit.Sts2.Core.Runs.IRunState? st, ulong netId)
        {
            try
            {
                if (st?.Players == null) return null;
                foreach (var p in st.Players) if (p.NetId == netId) return p;
            }
            catch { }
            return null;
        }

        private static RelicModel? FindRelic(MegaCrit.Sts2.Core.Entities.Players.Player player, string relicId)
        {
            try
            {
                foreach (var r in player.Relics)
                    if (string.Equals(r.Id?.Entry, relicId, System.StringComparison.OrdinalIgnoreCase)) return r;
            }
            catch { }
            return null;
        }
    }
}
