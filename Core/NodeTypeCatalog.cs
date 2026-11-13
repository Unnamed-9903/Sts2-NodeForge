using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Map;

namespace NodeForge
{
    /// <summary>What kind of forced model a hover option requires.</summary>
    public enum ForceKind { None = 0, Event = 1, Ancient = 2 }

    public sealed record NodeTypeOption(MapPointType PointType, string ConfigId, string LabelZh, string LabelEn, ForceKind Force = ForceKind.None)
    {
        public string Label => UiText.T(LabelZh, LabelEn);
        public bool IsEvent => Force == ForceKind.Event;
        public bool NeedsForced => Force != ForceKind.None;
    }

    /// <summary>
    /// Node type catalogue shared by both menus. MapPointType: Unassigned=0, Unknown=1, Shop=2,
    /// Treasure=3, RestSite=4, Monster=5, Elite=6, Boss=7, Ancient=8.
    /// </summary>
    internal static class NodeTypeCatalog
    {
        // Menu 1 (bulk replace) target types. Boss excluded.
        // NOTE: Ancient target removed — STS2 0.113's NNormalMapPoint cannot render an Ancient node
        // away from the start (IconName throws -> map freeze), so bulk-to-Ancient is disallowed.
        private static readonly NodeTypeOption[] Options =
        {
            new(MapPointType.Unknown,   "question",  "问号 (?)",   "Question (?)"),
            new(MapPointType.Monster,   "monster",   "普通战斗",   "Normal combat"),
            new(MapPointType.Elite,     "elite",     "精英",       "Elite"),
            new(MapPointType.RestSite,  "rest_site", "火堆",       "Campfire"),
            new(MapPointType.Shop,      "shop",      "商店",       "Shop"),
            new(MapPointType.Treasure,  "treasure",  "宝箱",       "Treasure"),
        };

        // Menu 2 (hover edit) target types. Ancient options removed for the same reason.
        private static readonly NodeTypeOption[] HoverOptions =
        {
            new(MapPointType.Unknown,   "question",      "问号 (?)",     "Question (?)"),
            new(MapPointType.Monster,   "monster",       "普通战斗",     "Normal combat"),
            new(MapPointType.Elite,     "elite",         "精英",         "Elite"),
            new(MapPointType.RestSite,  "rest_site",     "火堆",         "Campfire"),
            new(MapPointType.Shop,      "shop",          "商店",         "Shop"),
            new(MapPointType.Treasure,  "treasure",      "宝箱",         "Treasure"),
            new(MapPointType.Unknown,   "event",         "定向事件",     "Directed event", ForceKind.Event),
            // Directed ancient is implemented as an Unknown (event) node that forces an ancient event —
            // we cannot set the node type itself to Ancient (map would freeze in 0.113).
            new(MapPointType.Unknown,   "ancient_event", "定向远古",     "Directed ancient", ForceKind.Ancient),
        };

        public static IReadOnlyList<NodeTypeOption> All => Options;
        public static IReadOnlyList<NodeTypeOption> Hover => HoverOptions;

        // Menu 1 target list = normal types + "random ancient" (implemented as Unknown node + random ancient event).
        private static readonly NodeTypeOption[] _menu1Targets =
        {
            new(MapPointType.Unknown,   "question",  "问号 (?)",   "Question (?)"),
            new(MapPointType.Monster,   "monster",   "普通战斗",   "Normal combat"),
            new(MapPointType.Elite,     "elite",     "精英",       "Elite"),
            new(MapPointType.RestSite,  "rest_site", "火堆",       "Campfire"),
            new(MapPointType.Shop,      "shop",      "商店",       "Shop"),
            new(MapPointType.Treasure,  "treasure",  "宝箱",       "Treasure"),
            new(MapPointType.Unknown,   "random_ancient", "随机远古", "Random ancient", ForceKind.Ancient),
        };
        public static IReadOnlyList<NodeTypeOption> Menu1Targets => _menu1Targets;

        /// <summary>Resolve a Menu-1 target config id (supports random_ancient).</summary>
        public static string Menu1TargetLabel(string cfgId)
        {
            foreach (var o in _menu1Targets) if (string.Equals(o.ConfigId, cfgId, StringComparison.OrdinalIgnoreCase)) return o.Label;
            return cfgId;
        }

        public static string ToConfigId(MapPointType pointType)
        {
            foreach (var o in Options) if (o.PointType == pointType) return o.ConfigId;
            return "treasure";
        }

        public static string ToLabel(MapPointType pointType)
        {
            foreach (var o in Options) if (o.PointType == pointType) return o.Label;
            return pointType.ToString();
        }

        public static bool TryParseConfigId(string? configId, out MapPointType pointType)
        {
            foreach (var o in Options)
                if (string.Equals(o.ConfigId, configId, StringComparison.OrdinalIgnoreCase))
                {
                    pointType = o.PointType;
                    return true;
                }
            pointType = MapPointType.Unassigned;
            return false;
        }

        public static bool TryGetHoverOption(string? configId, out NodeTypeOption option)
        {
            foreach (var o in HoverOptions)
                if (string.Equals(o.ConfigId, configId, StringComparison.OrdinalIgnoreCase))
                {
                    option = o;
                    return true;
                }
            // random_ancient is a Menu-1 target but reuses the ancient-event path.
            foreach (var o in _menu1Targets)
                if (string.Equals(o.ConfigId, configId, StringComparison.OrdinalIgnoreCase))
                {
                    option = o;
                    return true;
                }
            option = HoverOptions[0];
            return false;
        }

        /// <summary>Map a hover config id to concrete MapPointType + its force kind.</summary>
        public static MapPointType HoverConfigIdToPointType(string? configId, out ForceKind force)
        {
            if (TryGetHoverOption(configId, out var o))
            {
                force = o.Force;
                return o.PointType;
            }
            force = ForceKind.None;
            return MapPointType.Monster;
        }
    }
}
