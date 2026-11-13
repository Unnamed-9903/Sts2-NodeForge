using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge
{
    /// <summary>
    /// Core map-node type conversion (original nodeswap feature, migrated and hardened).
    /// </summary>
    internal static class MapNodeService
    {
        /// <summary>Apply the Menu-1 rule to a map (used at generation and on-demand) and return a snapshot
        /// of what changed so it can be undone. Never touches boss or the starting ancient node.</summary>
        public static BulkSnapshot ConvertMap(ActMap? map)
        {
            var cfg = Settings.Current;
            if (cfg.EffectDisabled || map == null) return new BulkSnapshot { Label = "Auto" };
            // Unified with Menu 1 / Menu 2 bulk: same protection + same hover-edit path + marker effect.
            return BulkApply(map, null, t => MatchesMenu1Rule(t, cfg), cfg.TargetNode, "", "Auto");
        }

        public static void ConvertPointType(ref MapPointType pointType)
        {
            var cfg = Settings.Current;
            if (!cfg.EffectDisabled && ShouldConvert(pointType, cfg))
                pointType = cfg.TargetPointType;
        }

        public static bool MatchesMenu1Rule(MapPointType t, NodeForgeSettings cfg) => ShouldConvert(t, cfg);

        private static bool ShouldConvert(MapPointType t, NodeForgeSettings cfg)
        {
            // 0.113: a normal map node cannot be shown as Ancient (NNormalMapPoint.IconName has no
            // Ancient branch -> ArgumentOutOfRangeException -> map freeze). Never bulk-convert to Ancient.
            if (cfg.TargetPointType == MapPointType.Ancient) return false;
            return t switch
            {
                MapPointType.Unknown => cfg.ConvertQuestion,
                MapPointType.Elite => cfg.ConvertElite,
                MapPointType.Monster => cfg.ConvertMonster,
                MapPointType.Shop => cfg.ConvertShop,
                MapPointType.RestSite => cfg.ConvertRestSite,
                MapPointType.Treasure => cfg.ConvertTreasure,
                MapPointType.Ancient => false,   // never convert TO/OR FROM ancient via bulk
                _ => false,
            };
        }

        // ---- Hover edit (Menu 2) ----

        public static bool CanEdit(MapPoint point, IRunState? runState)
        {
            if (point == null) return false;
            if (point.PointType == MapPointType.Boss) return false;
            if (runState?.CurrentMapPoint != null &&
                runState.CurrentMapPoint.coord.Equals(point.coord)) return false;
            return true;
        }

        /// <summary>Whether the menu may be *opened* for this hovered point (stricter than CanEdit:
        /// excludes Boss, the map's original ancient start, the current node, and already-traveled nodes).</summary>
        public static bool MeetsOpenCondition(MapPoint? point, IRunState? runState, ActMap? map, out string reason, MapPointState state = MapPointState.None)
        {
            reason = "";
            if (point == null)
            {
                reason = UiText.T("请将鼠标悬浮在节点上", "Hover a map node first");
                return false;
            }
            if (state == MapPointState.Traveled)
            {
                reason = UiText.T("已走过的节点不可修改", "Traveled nodes cannot be edited");
                return false;
            }
            if (point.PointType == MapPointType.Boss)
            {
                reason = UiText.T("暂时不支持修改起点和终点（Boss）", "Boss / endpoint cannot be edited");
                return false;
            }
            // Original ancient start node.
            try
            {
                if (map != null && map.StartingMapPoint != null && map.StartingMapPoint.coord.Equals(point.coord))
                {
                    reason = UiText.T("暂时不支持修改起点和终点（起点先古之民）", "Starting ancient node cannot be edited");
                    return false;
                }
            }
            catch { }
            if (runState?.CurrentMapPoint != null && runState.CurrentMapPoint.coord.Equals(point.coord))
            {
                reason = UiText.T("当前所在节点不可修改", "Current node cannot be edited");
                return false;
            }
            return true;
        }

        /// <summary>Apply a hover edit and record history for undo. Returns record or null.</summary>
        public static NodeEditRecord? ApplyHoverEdit(MapPoint point, string hoverConfigId, string forceEventId, bool record = true)
        {
            var pt = NodeTypeCatalog.HoverConfigIdToPointType(hoverConfigId, out ForceKind force);

            NodeEditRecord? rec = null;
            if (record)
            {
                bool had = ForcedEventRegistry.TryGet(point.coord, out var prevId);
                rec = new NodeEditRecord(point.coord, point.PointType, had, prevId,
                    MapMarks.IsMarked(point.coord));
                EditHistory.Push(rec);
            }

            point.PointType = pt;
            if (force == ForceKind.Ancient)
            {
                // Resolve to a CONCRETE ancient id NOW (host-side) so the same id can be synced to clients
                // and both ends enter the SAME ancient. Empty forceEventId = "random" -> pick one concrete id.
                string ancientId = string.IsNullOrWhiteSpace(forceEventId)
                    ? MapNodeService.PickRandomAncientId()
                    : forceEventId!;
                ForcedEventRegistry.Set(point.coord, string.IsNullOrEmpty(ancientId) ? ForcedEventRegistry.RandomAncientToken : ancientId);
            }
            else if (force != ForceKind.None)
                ForcedEventRegistry.Set(point.coord, string.IsNullOrWhiteSpace(forceEventId) ? null : forceEventId);
            else
                ForcedEventRegistry.Remove(point.coord);

            MapMarks.MarkEdited(point.coord);
            return rec;
        }

        /// <summary>Pick a concrete ancient event id (for "random ancient").</summary>
        internal static string PickRandomAncientId()
        {
            try
            {
                var ids = new List<string>();
                foreach (var anc in MegaCrit.Sts2.Core.Models.ModelDb.AllAncients)
                {
                    try { var e = anc?.Id?.Entry; if (!string.IsNullOrEmpty(e)) ids.Add(e); } catch { }
                }
                if (ids.Count == 0) return "";
                int idx = Math.Abs(MegaCrit.Sts2.Core.Random.Rng.Chaotic.NextInt(ids.Count));
                return ids[idx];
            }
            catch { return ""; }
        }

        public static void Undo(NodeEditRecord rec, MapPoint point, ActMap? map = null, IRunState? runState = null)
        {
            var prot = CollectProtected(map, runState);
            if (prot.Contains(rec.Coord)) return;   // never modify protected nodes
            point.PointType = rec.PrevType;
            if (rec.HadForced) ForcedEventRegistry.Set(rec.Coord, rec.PrevForcedId);
            else ForcedEventRegistry.Remove(rec.Coord);
        }

        /// <summary>Unified bulk conversion used by BOTH Menu 1 (rule-based) and Menu 2 (single-source bulk).
        /// Applies the same protection (boss/start/current/traveled), the same hover-edit path (target type
        /// + optional forced event + marker effect), and records a single undoable snapshot.</summary>
        public static BulkSnapshot BulkApply(ActMap? map, IRunState? runState, Func<MapPointType, bool> shouldConvert,
            string targetCfgId, string forceEventId, string label = "Bulk", bool broadcast = false)
        {
            var snap = new BulkSnapshot { Label = label };
            if (map == null) return snap;
            var prot = CollectProtected(map, runState);
            var pts = new List<MapPoint>();
            try { foreach (var p in map.GetAllMapPoints()) pts.Add(p); } catch { }
            try { if (map.StartingMapPoint != null) pts.Add(map.StartingMapPoint); } catch { }

            var fk = NodeTypeCatalog.HoverConfigIdToPointType(targetCfgId, out var forceKind);
            byte forceByte = forceKind == ForceKind.Ancient ? (byte)2 : forceKind == ForceKind.Event ? (byte)1 : (byte)0;

            foreach (var p in pts)
            {
                if (p == null) continue;
                if (p.PointType == MapPointType.Boss) continue;
                if (prot.Contains(p.coord)) continue;
                // Never bulk-convert to/from Ancient (crashes NNormalMapPoint in 0.113).
                if (p.PointType == MapPointType.Ancient) continue;
                if (!shouldConvert(p.PointType)) continue;
                snap.Changed.Add((p.coord, p.PointType));
                string fid = forceByte == 2 ? "" : (forceByte == 1 ? forceEventId ?? "" : "");
                // Route through the unified command path (same as clients). SubmitApply mutates locally
                // and broadcasts if host; when not broadcasting (auto at map-gen) just apply locally.
                if (broadcast)
                    NodeForgeNet.SubmitApply(p.coord, fk, forceByte, fid);
                else
                    ApplyHoverEdit(p, targetCfgId, forceEventId ?? "", record: false);
            }
            return snap;
        }

        /// <summary>Coordinates that must never be modified: boss, starting ancient node, current node, traveled nodes.</summary>
        public static HashSet<MapCoord> CollectProtected(ActMap? map, IRunState? runState)
        {
            var set = new HashSet<MapCoord>();
            try { if (map != null) set.Add(map.BossMapPoint.coord); } catch { }
            try { if (map?.SecondBossMapPoint != null) set.Add(map.SecondBossMapPoint.coord); } catch { }
            try { if (map?.StartingMapPoint != null) set.Add(map.StartingMapPoint.coord); } catch { }
            try { if (runState?.CurrentMapPoint != null) set.Add(runState.CurrentMapPoint.coord); } catch { }
            // Traveled nodes: read each node's State from the live map screen dictionary.
            try
            {
                var screen = MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance;
                var f = typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen)
                    .GetField("_mapPointDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (screen != null && f?.GetValue(screen) is System.Collections.IDictionary dict)
                {
                    foreach (System.Collections.DictionaryEntry e in dict)
                    {
                        if (e.Value is MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapPoint np &&
                            np.State == MapPointState.Traveled)
                            set.Add((MapCoord)e.Key);
                    }
                }
            }
            catch (Exception ex) { Log.Warn("collect traveled: " + ex.Message); }
            return set;
        }

        /// <summary>Apply the Menu-1 rule to the current map NOW (not just future maps), snapshotting originals.</summary>
        public static BulkSnapshot ApplyRuleToCurrentMap(ActMap? map, NodeForgeSettings cfg, IRunState? runState = null)
        {
            var snap = new BulkSnapshot { Label = "Menu1" };
            if (map == null || cfg.EffectDisabled) return snap;
            var prot = CollectProtected(map, runState);
            var pts = new List<MapPoint>();
            try { foreach (var p in map.GetAllMapPoints()) pts.Add(p); } catch { }
            try { if (map.StartingMapPoint != null) pts.Add(map.StartingMapPoint); } catch { }

            foreach (var p in pts)
            {
                if (p == null) continue;
                if (p.PointType == MapPointType.Boss) continue;
                if (prot.Contains(p.coord)) continue;            // boss / start / current / traveled
                if (MapMarks.IsMarked(p.coord)) continue;        // respect explicit Menu-2 edits
                if (!ShouldConvert(p.PointType, cfg)) continue;
                snap.Changed.Add((p.coord, p.PointType));
                p.PointType = cfg.TargetPointType;
                MapMarks.MarkEdited(p.coord);
            }
            return snap;
        }

        /// <summary>Undo a bulk snapshot.</summary>
        /// <summary>Undo a bulk snapshot. Never touches protected nodes (boss / start / current / traveled).</summary>
        public static void UndoBulk(ActMap? map, BulkSnapshot snap, IRunState? runState = null)
        {
            if (map == null) return;
            var prot = CollectProtected(map, runState);
            foreach (var (coord, prev) in snap.Changed)
            {
                try
                {
                    if (prot.Contains(coord)) continue;   // do not modify protected nodes
                    var p = map.GetPoint(coord);
                    if (p != null) p.PointType = prev;
                }
                catch { }
            }
        }
    }
}
