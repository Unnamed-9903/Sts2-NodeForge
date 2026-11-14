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

        /// <summary>Actually perform the removal (official API) and grant points. Client notifies host.</summary>
        public static async void Commit(RelicModel relic, double value)
        {
            try
            {
                if (relic == null) return;
                // Official removal API.
                await MegaCrit.Sts2.Core.Commands.RelicCmd.Remove(relic);

                if (CombatExchange.IsAuthority)
                {
                    CombatExchange.AddPoints(value); // host pool
                }
                else
                {
                    // Client: notify host to add these points to the shared pool.
                    NodeForgeNet.SendSacrifice(value);
                }
                SyncToast.Show(UiText.T($"已献祭遗物，获得 {value:0} 点万象之源", $"Relic sacrificed; gained {value:0} EMC"));
            }
            catch (Exception e) { Log.Warn("sacrifice commit: " + e.Message); }
        }
    }
}
