using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge
{
    /// <summary>Provides the list of boss encounters for the current act (main + second), including
    /// mod-added bosses (they are merged into the act's encounter set).</summary>
    internal static class BossCatalog
    {
        public sealed class BossOption
        {
            public string Id = "";
            public string Title = "";
            public EncounterModel? Encounter;
            public bool IsCurrent;
            public bool IsSecond;
            /// <summary>Static thumbnail texture path (may not exist for spine-only bosses).</summary>
            public string ThumbPath = "";
        }

        /// <summary>All boss encounters selectable for the current act.</summary>
        public static List<BossOption> List(bool second)
        {
            var result = new List<BossOption>();
            try
            {
                var st = RunManager.Instance?.DebugOnlyGetState();
                var act = st?.Act;
                if (act == null) return result;
                string curId = "";
                try { curId = second ? (act.SecondBossEncounter?.Id.Entry ?? "") : act.BossEncounter.Id.Entry; } catch { }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var enc in act.AllBossEncounters)
                {
                    if (enc == null) continue;
                    string id = "";
                    try { id = enc.Id?.Entry ?? ""; } catch { }
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                    var o = new BossOption { Id = id, Encounter = enc, IsSecond = second };
                    try { o.Title = enc.Title?.GetRawText() ?? id; } catch { o.Title = id; }
                    try { o.ThumbPath = enc.BossNodePath + ".png"; } catch { }
                    o.IsCurrent = string.Equals(id, curId, StringComparison.OrdinalIgnoreCase);
                    result.Add(o);
                }
                result.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e) { Log.Warn("boss catalog: " + e.Message); }
            return result;
        }

        public static EncounterModel? Resolve(string id)
        {
            try
            {
                foreach (var enc in ModelDb.AllEncounters)
                {
                    try { if (enc.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Boss && string.Equals(enc.Id.Entry, id, StringComparison.OrdinalIgnoreCase)) return enc; }
                    catch { }
                }
            }
            catch { }
            return null;
        }
    }
}
