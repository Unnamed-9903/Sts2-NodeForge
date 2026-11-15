using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge.Patches
{
    /// <summary>Track the hovered point on the Blitz (top-bar linear) map, so the editor can open for it.</summary>
    [HarmonyPatch(typeof(NBlitzTopBarMapPoint), "OnFocus")]
    internal static class NBlitzMapPointOnFocusPatch
    {
        private static void Postfix(NBlitzTopBarMapPoint __instance)
        {
            try
            {
                var rs = Traverse.Create(__instance).Field("_runState").GetValue<IRunState>();
                var point = __instance.Point;
                HoverState.Focus(point, rs, BlitzState.Of(point, rs));
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(NBlitzTopBarMapPoint), "OnUnfocus")]
    internal static class NBlitzMapPointOnUnfocusPatch
    {
        private static void Postfix(NBlitzTopBarMapPoint __instance)
        {
            try { HoverState.Unfocus(__instance.Point); }
            catch { }
        }
    }

    internal static class BlitzState
    {
        /// <summary>Infer a MapPointState for a blitz point: current/prior nodes are non-editable.</summary>
        public static MapPointState Of(MapPoint point, IRunState? rs)
        {
            try
            {
                if (rs == null || point == null) return MapPointState.None;
                var cur = rs.MapLocation.coord;
                if (cur == null) return MapPointState.Travelable;
                var cc = cur.Value;
                if (point.coord == cc) return MapPointState.Traveled; // current -> not editable
                // Compare by row (linear map): rows < current row are past.
                if (point.coord.row < cc.row) return MapPointState.Traveled;
                return MapPointState.Travelable;
            }
            catch { return MapPointState.Travelable; }
        }
    }
}
