using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;

namespace NodeForge.Patches
{
    /// <summary>On a freshly generated map: only RESET per-run state. There is intentionally NO automatic
    /// bulk conversion anymore — replacements are applied only by the player, in-run, via the windows.
    /// (Stability first: no persistent auto-replace.)</summary>
    [HarmonyPatch(typeof(Hook), "ModifyGeneratedMap")]
    internal static class HookModifyGeneratedMapPatch
    {
        private static void Postfix(ref ActMap __result)
        {
            ForcedEventRegistry.Clear();
            MapMarks.Clear();
            EditHistory.Clear();
            BulkHistory.Clear();
        }
    }

    /// <summary>When the map screen is (re)built, just re-draw rings. Do NOT re-apply the bulk rule here —
    /// that would clobber Menu-2 edits / undos every time the map reopens.</summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen), "SetMap",
        new[] { typeof(ActMap), typeof(ulong), typeof(bool) })]
    internal static class NMapScreenSetMapPatch
    {
        private static void Postfix()
        {
            try { MapMarks.RedrawAll(); }
            catch (System.Exception e) { Log.Warn("RedrawAll: " + e.Message); }
        }
    }

    // NOTE: no EnterMapPointInternal conversion patch — the point type is already resolved from the
    // live MapPoint at entry time. Re-applying the bulk rule there would override Menu-2 edits and
    // undos (a node would "revert" to the replaced type on the way in). Removed intentionally.
}
