using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge.Patches
{
    /// <summary>
    /// Force a specific event (or a specific ancient, as an event) when entering a node marked with a
    /// forced id. PullNextEvent is where the game resolves an Event room's event. Ancients are resolved
    /// from ModelDb.AllAncients and returned as EventModel — the node stays 'Unknown' (question) so the
    /// map screen never tries to render an Ancient marker (which would crash in 0.113).
    /// </summary>
    [HarmonyPatch(typeof(ActModel), "PullNextEvent")]
    internal static class ActModelPullNextEventForcedPatch
    {
        private static void Postfix(ActModel __instance, RunState runState, ref EventModel __result)
        {
            try
            {
                var mp = runState?.CurrentMapPoint;
                if (mp == null) return;
                if (!ForcedEventRegistry.TryGet(mp.coord, out var forcedId)) return;
                if (string.IsNullOrEmpty(forcedId)) return; // random normal event

                EventModel? model;
                if (forcedId == ForcedEventRegistry.RandomAncientToken)
                    model = PickRandomAncient();
                else
                    model = ResolveEvent(forcedId!);
                if (model != null) __result = model;
            }
            catch (Exception e) { Log.Warn("forced event postfix: " + e.Message); }
        }

        /// <summary>Pick an ancient for a "random ancient" node. Uses a chaotic RNG so it differs each entry.</summary>
        internal static EventModel? PickRandomAncient()
        {
            try
            {
                var list = new System.Collections.Generic.List<EventModel>();
                foreach (var anc in ModelDb.AllAncients) { if (anc != null) list.Add(anc); }
                if (list.Count == 0) return null;
                int idx = Math.Abs(MegaCrit.Sts2.Core.Random.Rng.Chaotic.NextInt(list.Count));
                return list[idx];
            }
            catch (Exception e) { Log.Warn("PickRandomAncient: " + e.Message); return null; }
        }

        internal static EventModel? ResolveEvent(string id)
        {
            try
            {
                foreach (var ev in ModelDb.AllEvents)
                {
                    try { if (string.Equals(ev.Id.Entry, id, StringComparison.OrdinalIgnoreCase)) return ev; }
                    catch { }
                }
                // Ancients are not in AllEvents.
                foreach (var anc in ModelDb.AllAncients)
                {
                    try { if (string.Equals(anc.Id.Entry, id, StringComparison.OrdinalIgnoreCase)) return anc; }
                    catch { }
                }
            }
            catch (Exception e) { Log.Warn("ResolveEvent failed: " + e.Message); }
            return null;
        }
    }
}
