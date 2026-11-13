using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge.Patches
{
    /// <summary>
    /// Makes each ancient NODE's options differ: before generating options, replace the event's Rng with
    /// one seeded from the current map coordinate (+ act + run seed). Without this, every instance of the
    /// same ancient uses the same seed (= same options). Uses the existing Rng(ulong) ctor (no removed APIs).
    /// </summary>
    [HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
    internal static class AncientOptionVariationPatch
    {
        private static readonly AccessTools.FieldRef<EventModel, Rng> RngRef =
            AccessTools.FieldRefAccess<EventModel, Rng>("<Rng>k__BackingField");

        private static void Prefix(AncientEventModel __instance)
        {
            try
            {
                // NEVER vary the Rng in multiplayer: host & client must generate identical ancient options,
                // otherwise the event option indices mismatch and the event (e.g. VAKUU) hangs on a black screen.
                if (NetAuth.IsMultiplayer()) return;

                var ev = (EventModel)__instance;
                var st = ev.Owner?.RunState;
                var mp = st?.CurrentMapPoint;
                if (st == null || mp == null) return;

                int row = mp.coord.row, col = mp.coord.col, act = st.CurrentActIndex;
                ulong seed = st.Rng.Seed
                             + (ulong)(act * 7919 + row * 1009 + col * 313)
                             + (ulong)StringComparer.OrdinalIgnoreCase.GetHashCode(ev.Id?.Entry ?? "");
                // Deterministic per-node but different across nodes (singleplayer only).
                RngRef(ev) = new Rng(seed);
            }
            catch (Exception e) { Log.Warn("ancient option variation: " + e.Message); }
        }
    }
}
