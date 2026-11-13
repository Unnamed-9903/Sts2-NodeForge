using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;

namespace NodeForge
{
    /// <summary>
    /// Maps a map coordinate to a forced event model id (empty/null value = "random event").
    /// Cleared whenever a new map is generated so stale coords never leak across acts.
    /// </summary>
    internal static class ForcedEventRegistry
    {
        /// <summary>Special stored value meaning "pick a random ancient on each entry".</summary>
        public const string RandomAncientToken = "__nodeforge_random_ancient__";

        private static readonly Dictionary<MapCoord, string?> _map = new();

        public static void Clear() => _map.Clear();

        public static void Set(MapCoord coord, string? eventId) => _map[coord] = eventId;

        public static void Remove(MapCoord coord) => _map.Remove(coord);

        public static bool TryGet(MapCoord coord, out string? eventId) => _map.TryGetValue(coord, out eventId);

        public static bool Has(MapCoord coord) => _map.ContainsKey(coord);
    }
}
