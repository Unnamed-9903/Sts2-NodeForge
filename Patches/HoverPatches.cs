using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge.Patches
{
    /// <summary>Track which map node the mouse is over (base class methods cover all node kinds).</summary>
    [HarmonyPatch(typeof(NMapPoint), "OnFocus")]
    internal static class NMapPointOnFocusPatch
    {
        private static void Postfix(NMapPoint __instance)
        {
            try
            {
                IRunState? rs = Traverse.Create(__instance).Field("_runState").GetValue<IRunState>();
                HoverState.Focus(__instance.Point, rs, __instance.State);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(NMapPoint), "OnUnfocus")]
    internal static class NMapPointOnUnfocusPatch
    {
        private static void Postfix(NMapPoint __instance)
        {
            try { HoverState.Unfocus(__instance.Point); }
            catch { }
        }
    }
}
