using System;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge
{
    /// <summary>Change the current act's boss (main/second) via the official API, optionally switching the
    /// Dread "Dominion" (nightmare boss buff) to the new boss's dominion.</summary>
    internal static class BossService
    {
        /// <summary>Host/authority: apply boss change locally and broadcast to clients (for sync).</summary>
        public static bool ApplyAndSync(bool second, string bossId, bool syncDominion, out string err)
        {
            err = "";
            try
            {
                var st = RunManager.Instance?.DebugOnlyGetState();
                if (st == null) { err = "not in run"; return false; }
                if (!ApplyLocal(st, second, bossId, syncDominion, out err)) return false;
                NodeForgeNet.SendBossChange(second, bossId, syncDominion);
                return true;
            }
            catch (Exception e) { err = e.Message; Log.Warn("apply boss: " + e.Message); return false; }
        }

        /// <summary>Apply a boss change to the given run state (no networking). Used locally on host and on
        /// clients when a host command is received.</summary>
        public static bool ApplyLocal(IRunState st, bool second, string bossId, bool syncDominion, out string err)
        {
            err = "";
            try
            {
                var enc = BossCatalog.Resolve(bossId);
                if (enc == null) { err = "boss not found: " + bossId; return false; }
                if (enc.RoomType != MegaCrit.Sts2.Core.Rooms.RoomType.Boss) { err = "not a boss"; return false; }

                // Eagerly load this boss's map-node assets (spine/placeholder png) so the rebuilt Boss node
                // can render its icon. These are normally preloaded per-act, but a swapped-in boss may not be.
                try
                {
                    foreach (var path in enc.MapNodeAssetPaths)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(path))
                                MegaCrit.Sts2.Core.Assets.PreloadManager.Cache.GetAsset<Godot.Resource>(path);
                        }
                        catch (Exception e) { Log.Warn("preload boss asset " + path + ": " + e.Message); }
                    }
                }
                catch (Exception e) { Log.Warn("preload boss assets: " + e.Message); }

                var act = st.Act;
                if (second)
                {
                    if (!act.HasSecondBoss) { err = "no second boss"; return false; }
                    act.SetSecondBossEncounter(enc);
                    // MapCmd.SetSecondBossEncounter doesn't exist; refresh UI manually.
                    try
                    {
                        if (MegaCrit.Sts2.Core.TestSupport.TestMode.IsOff)
                        {
                            MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi?.TopBar?.BossIcon?.RefreshBossIcon();
                            RunManager.Instance?.InvokeMapChanged(false);
                        }
                    }
                    catch (Exception e) { Log.Warn("refresh second boss ui: " + e.Message); }
                }
                else
                {
                    MapCmd.SetBossEncounter(st, enc);
                }

                if (syncDominion)
                {
                    try
                    {
                        var dom = enc.GetDominion();
                        if (st is RunState rst) rst.SetDominion(dom);
                    }
                    catch (Exception e) { Log.Warn("set dominion: " + e.Message); }
                }
                Log.Info($"boss changed: second={second} -> {bossId} (dominionSync={syncDominion})");
                // Force a static icon on the rebuilt boss node (Spine art may be blank after a swap).
                try { BossNodeIconFallback.ApplyDeferred(); } catch { }
                return true;
            }
            catch (Exception e) { err = e.Message; Log.Warn("apply boss local: " + e.Message); return false; }
        }
    }
}
