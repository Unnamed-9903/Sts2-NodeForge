using System;
using System.IO;
using System.Text;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.Sidecar;

namespace NodeForge
{
    /// <summary>
    /// Host→client sync of map-node edits via RitsuLib's Sidecar channel (the official mod-networking path).
    /// The host applies an edit locally AND broadcasts one command per edit; each client applies the same
    /// command to its own local map so both ends stay consistent. Clients never edit on their own.
    /// </summary>
    internal static class NodeForgeNet
    {
        // Stable 64-bit opcode for NodeForge node-edit commands.
        private const ulong Opcode = 0x4E46_534E_4F44UL; // "NFSNOD"
        // Opcode for "a client sacrificed a relic worth N points" (client -> host).
        private const ulong SacrificeOpcode = 0x4E46_5341_4352UL; // "NFSACR"
        // Opcode for pool state broadcast (host -> clients), for read-only display.
        private const ulong PoolOpcode = 0x4E46_504F_4F4CUL; // "NFPOOL"

        private static bool _registered;

        internal enum Op : byte { Apply = 0, Revert = 1 }

        public sealed class Command
        {
            public Op Kind;
            public int Col;
            public int Row;
            public int PointType;      // (int)MapPointType
            public byte ForceKind;     // 0 none, 1 event, 2 ancient(random token)
            public string ForcedId = "";
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            try
            {
                RitsuLibSidecarBus.RegisterHandler(Opcode, OnReceive);
                RitsuLibSidecarBus.RegisterHandler(SacrificeOpcode, OnReceiveSacrifice);
                RitsuLibSidecarBus.RegisterHandler(PoolOpcode, OnReceivePool);
                Log.Info("net: sidecar handler registered (opcode " + Opcode + ", " + SacrificeOpcode + ", " + PoolOpcode + ")");
            }
            catch (Exception e) { Log.Warn("net: register handler failed: " + e.Message); }
        }

        // Coords whose last command failed to broadcast (no ready peer yet). Re-flushed on next enter/resync.
        private static readonly System.Collections.Generic.Dictionary<long, Command> _pending = new();

        private static long Key(MapCoord c) => ((long)c.col << 32) ^ (uint)c.row;

        /// <summary>Client: tell the host that this player sacrificed a relic worth `points` (added to host pool).</summary>
        public static void SendSacrifice(double points)
        {
            try
            {
                var rm = RunManager.Instance;
                if (rm?.NetService == null) return;
                if (rm.NetService.Type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Client) return;
                byte[] payload = BitConverter.GetBytes(points);
                bool ok = RitsuLibSidecarHighLevelSend.TrySendAsClient(rm, SacrificeOpcode, payload,
                    RitsuLibSidecarDeliverySemantics.StableSync);
                if (!ok) Log.Warn("net: send sacrifice failed");
            }
            catch (Exception e) { Log.Warn("net: send sacrifice: " + e.Message); }
        }

        private static void OnReceiveSacrifice(RitsuLibSidecarDispatchContext ctx)
        {
            try
            {
                // Only the host ingests client sacrifice messages (added to the shared host pool).
                if (!CombatExchange.IsAuthority) return;
                var bytes = ctx.Payload.ToArray();
                if (bytes.Length < sizeof(double)) return;
                double pts = BitConverter.ToDouble(bytes, 0);
                CombatExchange.AddPoints(pts);
                Log.Info($"net: client sacrifice +{pts} (pool now {CombatExchange.Points})");
            }
            catch (Exception e) { Log.Warn("net: recv sacrifice: " + e.Message); }
        }

        /// <summary>Host: broadcast the current pool value so clients can display the real balance.</summary>
        public static void BroadcastPool()
        {
            try
            {
                var rm = RunManager.Instance;
                if (rm?.NetService?.Type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host) return;
                byte[] payload = BitConverter.GetBytes(CombatExchange.Points);
                RitsuLibSidecarHighLevelSend.TrySendAsHostBroadcast(rm, PoolOpcode, payload,
                    RitsuLibSidecarDeliverySemantics.StableSync);
            }
            catch (Exception e) { Log.Warn("net: broadcast pool: " + e.Message); }
        }

        private static void OnReceivePool(RitsuLibSidecarDispatchContext ctx)
        {
            try
            {
                if (CombatExchange.IsAuthority) return; // host ignores
                var bytes = ctx.Payload.ToArray();
                if (bytes.Length < sizeof(double)) return;
                CombatExchange.SetClientView(BitConverter.ToDouble(bytes, 0));
            }
            catch (Exception e) { Log.Warn("net: recv pool: " + e.Message); }
        }

        /// <summary>Host: broadcast the CURRENT local state of a coord (idempotent). Used as a safety
        /// resync right before entering a node, so a lost earlier command can't desync the room.</summary>
        public static void HostResync(MapCoord coord)
        {
            try
            {
                var net = RunManager.Instance?.NetService;
                if (net?.Type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host) return;
                var map = RunManager.Instance?.DebugOnlyGetState()?.Map;
                if (map == null) return;

                // Flush any commands whose earlier broadcast failed.
                FlushPending();

                var p = map.GetPoint(coord);
                if (p == null) return;

                byte fk = 0;
                string fid = "";
                if (ForcedEventRegistry.TryGet(coord, out var eid) && !string.IsNullOrEmpty(eid))
                {
                    fid = eid!;
                    fk = eid == ForcedEventRegistry.RandomAncientToken ? (byte)2 : (byte)1;
                    if (fk == 1 && IsAncientId(eid!)) fk = 2;
                }
                // Only resync nodes that are actually forced (event/ancient). Broadcasting for ordinary
                // nodes would compete with the game's own map-move messages and can stall clients.
                if (fk == 0) return;
                Broadcast(Op.Apply, coord, (int)p.PointType, fk, fid);
            }
            catch (Exception e) { Log.Warn("net: resync failed: " + e.Message); }
        }

        private static bool IsAncientId(string id)
        {
            try
            {
                foreach (var a in MegaCrit.Sts2.Core.Models.ModelDb.AllAncients)
                    if (string.Equals(a?.Id?.Entry, id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Submit an edit command. It is ALWAYS executed locally through the same ApplyLocal() path a client
        /// uses (so singleplayer uses the exact same command model as remote sync), then — if we are the
        /// host — broadcast to clients.
        /// </summary>
        public static void SubmitApply(MapCoord coord, MapPointType pointType, byte forceKind, string forcedId)
        {
            // Resolve "random ancient" to ONE concrete id here so host & clients use the SAME ancient.
            if (forceKind == 2 && string.IsNullOrEmpty(forcedId))
                forcedId = MapNodeService.PickRandomAncientId();
            ApplyLocal(new Command { Kind = Op.Apply, Col = coord.col, Row = coord.row, PointType = (int)pointType, ForceKind = forceKind, ForcedId = forcedId ?? "" });
            var net = RunManager.Instance?.NetService;
            if (net?.Type == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host)
            {
                var cmd = new Command { Kind = Op.Apply, Col = coord.col, Row = coord.row, PointType = (int)pointType, ForceKind = forceKind, ForcedId = forcedId ?? "" };
                bool ok = Broadcast(Op.Apply, coord, (int)pointType, forceKind, forcedId ?? "");
                if (ok) _pending.Remove(Key(coord)); else _pending[Key(coord)] = cmd;
                // (no per-command toast spam for failures; the enter-time resync will retry)
            }
            else
            {
                SyncToast.Show(UiText.T($"已修改节点 ({coord.col},{coord.row}) → {NodeTypeCatalog.ToLabel(pointType)}", $"Changed node ({coord.col},{coord.row}) -> {NodeTypeCatalog.ToLabel(pointType)}"));
            }
        }

        /// <summary>Re-broadcast all commands whose earlier broadcast failed (peer became ready).</summary>
        private static void FlushPending()
        {
            if (_pending.Count == 0) return;
            var snapshot = new System.Collections.Generic.List<Command>(_pending.Values);
            foreach (var c in snapshot)
            {
                var coord = new MapCoord { col = c.Col, row = c.Row };
                if (Broadcast(c.Kind, coord, c.PointType, c.ForceKind, c.ForcedId ?? ""))
                    _pending.Remove(Key(coord));
            }
        }

        /// <summary>Submit a revert (undo) command; executed locally then broadcast if host.</summary>
        public static void SubmitRevert(MapCoord coord, MapPointType prevType, bool hadForced, string? prevForcedId, byte prevForceKind)
        {
            byte fk = hadForced ? prevForceKind : (byte)0;
            ApplyLocal(new Command { Kind = Op.Revert, Col = coord.col, Row = coord.row, PointType = (int)prevType, ForceKind = fk, ForcedId = prevForcedId ?? "" });
            var net = RunManager.Instance?.NetService;
            if (net?.Type == MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host)
            {
                bool ok = Broadcast(Op.Revert, coord, (int)prevType, fk, prevForcedId ?? "");
                if (ok) _pending.Remove(Key(coord));
                else _pending[Key(coord)] = new Command { Kind = Op.Revert, Col = coord.col, Row = coord.row, PointType = (int)prevType, ForceKind = fk, ForcedId = prevForcedId ?? "" };
            }
            else
            {
                SyncToast.Show(UiText.T($"已还原节点 ({coord.col},{coord.row})", $"Reverted node ({coord.col},{coord.row})"));
            }
        }

        /// <summary>Broadcast a command to ready clients (host only).</summary>
        private static bool Broadcast(Op kind, MapCoord coord, int pointType, byte forceKind, string forcedId)
        {
            try
            {
                var rm = RunManager.Instance;
                if (rm?.NetService?.Type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Host) return false;
                byte[] payload = Encode(kind, coord, pointType, forceKind, forcedId);
                bool ok = RitsuLibSidecarHighLevelSend.TrySendAsHostBroadcast(rm, Opcode, payload,
                    RitsuLibSidecarDeliverySemantics.StableSync);
                if (!ok) Log.Warn("net: broadcast failed for " + kind + " " + coord);
                return ok;
            }
            catch (Exception e) { Log.Warn("net: broadcast failed: " + e.Message); return false; }
        }

        private static byte[] Encode(Op kind, MapCoord coord, int pointType, byte forceKind, string forcedId)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms, Encoding.UTF8);
            w.Write((byte)kind);
            w.Write(coord.col);
            w.Write(coord.row);
            w.Write(pointType);
            w.Write(forceKind);
            w.Write(forcedId ?? "");
            w.Flush();
            return ms.ToArray();
        }

        private static Command Decode(ReadOnlyMemory<byte> payload)
        {
            using var ms = new MemoryStream(payload.ToArray());
            using var r = new BinaryReader(ms, Encoding.UTF8);
            var c = new Command
            {
                Kind = (Op)r.ReadByte(),
                Col = r.ReadInt32(),
                Row = r.ReadInt32(),
                PointType = r.ReadInt32(),
                ForceKind = r.ReadByte(),
                ForcedId = r.ReadString(),
            };
            return c;
        }

        private static void OnReceive(RitsuLibSidecarDispatchContext ctx)
        {
            try
            {
                // Only accept commands that the HOST sent to us (clients execute, never originate).
                var net = RunManager.Instance?.NetService;
                var type = net?.Type;
                if (type != MegaCrit.Sts2.Core.Multiplayer.Game.NetGameType.Client) return; // host ignores echoes

                var cmd = Decode(ctx.Payload);
                ApplyLocal(cmd);
                SyncToast.Show(UiText.T(
                    $"主机{(cmd.Kind == Op.Revert ? "还原" : "修改")}了节点 ({cmd.Col},{cmd.Row}) → {NodeTypeCatalog.ToLabel((MapPointType)cmd.PointType)}",
                    $"Host {(cmd.Kind == Op.Revert ? "reverted" : "changed")} node ({cmd.Col},{cmd.Row}) -> {NodeTypeCatalog.ToLabel((MapPointType)cmd.PointType)}"));
            }
            catch (Exception e) { Log.Warn("net: receive failed: " + e.Message); }
        }

        /// <summary>Client: apply a host command to the local map (no protection checks — host is authority).</summary>
        private static void ApplyLocal(Command cmd)
        {
            try
            {
                var st = RunManager.Instance?.DebugOnlyGetState();
                var map = st?.Map;
                if (map == null) { SyncToast.Show(UiText.T("收到主机节点修改，但本地地图未就绪（可能不同步）", "Got host node change but local map not ready (may be out of sync)")); return; }
                var coord = new MapCoord { col = cmd.Col, row = cmd.Row };
                MapPoint? p = null;
                try { p = map.GetPoint(coord); } catch { }
                if (p == null) { Log.Warn($"net: coord {cmd.Col},{cmd.Row} not on local map, skip"); SyncToast.Show(UiText.T($"收到主机节点修改 ({cmd.Col},{cmd.Row})，但本地无该节点（不同步）", $"Got host node change ({cmd.Col},{cmd.Row}) but no such local node (out of sync)")); return; }

                p.PointType = (MapPointType)cmd.PointType;

                byte fk = cmd.ForceKind;
                if (fk == 0) ForcedEventRegistry.Remove(coord);
                else if (fk == 2)
                    // Use the host's concrete ancient id when provided; token only as legacy fallback.
                    ForcedEventRegistry.Set(coord, string.IsNullOrEmpty(cmd.ForcedId) ? ForcedEventRegistry.RandomAncientToken : cmd.ForcedId);
                else ForcedEventRegistry.Set(coord, string.IsNullOrEmpty(cmd.ForcedId) ? null : cmd.ForcedId);

                // Refresh the node's visuals so the client sees it immediately.
                try
                {
                    var screen = MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance;
                    var f = typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen)
                        .GetField("_mapPointDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (screen != null && f?.GetValue(screen) is System.Collections.IDictionary dict && dict.Contains(coord))
                    {
                        var node = dict[coord];
                        var m = node?.GetType().GetMethod("RefreshVisualsInstantly",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                        m?.Invoke(node, null);
                    }
                }
                catch { }
                // Revert removes the particle mark; Apply (re)adds it.
                try { if (cmd.Kind == Op.Revert) MapMarks.Unmark(coord); else MapMarks.MarkEdited(coord); } catch { }
                Log.Info($"net: applied {cmd.Kind} at {cmd.Col},{cmd.Row} -> {(MapPointType)cmd.PointType} (force {cmd.ForceKind})");
            }
            catch (Exception e) { Log.Warn("net: apply local failed: " + e.Message); }
        }
    }
}

