using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge.Patches
{
    /// <summary>
    /// Nodes NodeForge marked with a forced event/ancient use PointType=Unknown; the game then randomly
    /// rolls a room type for Unknown nodes (sometimes Monster -> the "2nd ancient becomes a fight" issue).
    /// We record the coord being entered and force such nodes to be an Event room.
    /// </summary>
    [HarmonyPatch(typeof(RunManager), "EnterMapCoordInternal")]
    internal static class EnterMapCoordRecordPatch
    {
        private static void Prefix(MapCoord coord)
        {
            ForcedRoomForce.EnteringCoord = coord;
            // Force-close any NodeForge overlay before entering a node/room, so a lingering full-screen
            // backdrop can never block the next screen (e.g. the merchant's card-removal card selection).
            try { Menu1Overlay.CloseIfOpen(); Menu2Overlay.CloseIfOpen(); } catch { }
            // Host: re-broadcast this node's CURRENT state right before entering it, so clients are
            // guaranteed in sync even if an earlier command was lost (idempotent).
            try { NodeForgeNet.HostResync(coord); } catch { }
        }
    }

    [HarmonyPatch(typeof(RunManager), "RollRoomTypeFor")]
    internal static class RollRoomTypeForcedEventPatch
    {
        private static void Postfix(ref RoomType __result)
        {
            try
            {
                MapCoord? c = ForcedRoomForce.EnteringCoord;
                // Also consider the current map point (more robust across entry paths).
                try
                {
                    var cur = RunManager.Instance?.DebugOnlyGetState()?.CurrentMapPoint?.coord;
                    if (cur.HasValue && (c == null || !ForcedEventRegistry.Has(c.Value)))
                        c = cur;
                }
                catch { }
                if (c == null) return;
                if (ForcedEventRegistry.TryGet(c.Value, out var id) && !string.IsNullOrEmpty(id))
                    __result = RoomType.Event;
            }
            catch (Exception e) { Log.Warn("force event room: " + e.Message); }
        }
    }

    internal static class ForcedRoomForce
    {
        public static MapCoord? EnteringCoord;
    }
}
