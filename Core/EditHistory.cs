using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;

namespace NodeForge
{
    internal sealed record NodeEditRecord(
        MapCoord Coord,
        MapPointType PrevType,
        bool HadForced,
        string? PrevForcedId,
        bool WasMarked);

    /// <summary>Stores the history of Menu 2 edits so the player can undo the last change.</summary>
    internal static class EditHistory
    {
        private static readonly Stack<NodeEditRecord> _stack = new();

        public static void Push(NodeEditRecord r) => _stack.Push(r);

        public static bool CanUndo => _stack.Count > 0;

        public static NodeEditRecord? Pop() => _stack.Count > 0 ? _stack.Pop() : null;

        public static void Clear() => _stack.Clear();
    }

    /// <summary>A batch of node changes (from Menu 1 bulk apply or Menu 2 single-map bulk) that can be undone as a unit.</summary>
    internal sealed class BulkSnapshot
    {
        public string Label = "";
        public List<(MapCoord Coord, MapPointType PrevType)> Changed = new();
    }

    /// <summary>Undo stack for bulk replacements (Menu 1 "apply to current map" and Menu 2 single-map bulk).</summary>
    internal static class BulkHistory
    {
        private static readonly Stack<BulkSnapshot> _stack = new();

        public static void Push(BulkSnapshot snap) { if (snap.Changed.Count > 0) _stack.Push(snap); }

        public static bool CanUndo => _stack.Count > 0;

        public static BulkSnapshot? Pop() => _stack.Count > 0 ? _stack.Pop() : null;

        public static void Clear() => _stack.Clear();
    }
}
