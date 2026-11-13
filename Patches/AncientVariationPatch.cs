using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;

namespace NodeForge.Patches
{
    /// <summary>
    /// FIXED ancient variation. The original mod rewrote EventModel.Rng in GenerateInitialOptionsWrapper
    /// using Rng.Seed and a Rng(uint,int) ctor that no longer exist (caused MissingMethodException and
    /// the ancient dialogue freeze). This version instead varies WHICH ancient is pulled, using the
    /// still-existing RunState.Rng.Seed (ulong) and Rng(ulong) ctor, and never touches EventModel.Rng.
    /// </summary>
    [HarmonyPatch(typeof(ActModel), "PullAncient")]
    internal static class ActModelPullAncientVariationPatch
    {
        private static void Postfix(ActModel __instance, ref EventModel __result)
        {
            var cfg = Settings.Current;
            if (cfg.EffectDisabled || cfg.TargetPointType != MapPointType.Ancient || !cfg.VaryAncientByFloor)
                return;

            try
            {
                var rm = RunManager.Instance;
                var state = rm?.DebugOnlyGetState();
                var mp = state?.CurrentMapPoint;
                if (state == null || mp == null) return;

                var candidates = GetUnlockedAncients(state.UnlockState);
                if (candidates.Count <= 1) return;

                int row = mp.coord.row, col = mp.coord.col, act = state.CurrentActIndex;
                ulong seed = state.Rng.Seed + (ulong)(act * 7919 + row * 1009 + col * 313);
                var pick = new Rng(seed);
                int index = Math.Abs(pick.NextInt(candidates.Count));
                __result = candidates[index];
            }
            catch (Exception e) { Log.Warn("ancient variation: " + e.Message); }
        }

        private static List<EventModel> GetUnlockedAncients(UnlockState unlockState)
        {
            return ModelDb.Acts
                .SelectMany(a => a.GetUnlockedAncients(unlockState))
                .Concat(unlockState.SharedAncients)
                .GroupBy(x => x.Id)
                .Select(g => (EventModel)g.First())
                .OrderBy(x => x.Id.Entry, StringComparer.Ordinal)
                .ToList();
        }
    }
}
