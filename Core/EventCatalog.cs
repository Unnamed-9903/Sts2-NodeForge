using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

namespace NodeForge
{
    public sealed class EventOptionInfo
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class RelicInfo
    {
        public string Id { get; set; } = "";       // ModelId.Entry, for looking up the icon
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public enum EventColorClass { Green = 0, Orange = 1, Yellow = 2, Red = 3, Missing = 4 }

    public sealed class EventInfo
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<EventOptionInfo> Options { get; set; } = new();
        public List<RelicInfo> Relics { get; set; } = new();   // for ancients: all possible relics
        public bool IsAncient { get; set; }
        public bool IsMissing { get; set; }   // was in cache but no longer registered (mod removed)

        // --- static metadata (cached) ---
        public bool IsShared { get; set; }
        public List<int> ActIndices { get; set; } = new();   // act indices (0-based) this event belongs to
        public bool IsModEvent { get; set; }
        public string SourceModId { get; set; } = "";
        public string SourceModName { get; set; } = "";

        public string? Error { get; set; }

        // --- dynamic (computed per run at display time) ---
        [System.Text.Json.Serialization.JsonIgnore]
        public EventColorClass ColorClass { get; set; } = EventColorClass.Green;
        [System.Text.Json.Serialization.JsonIgnore]
        public string ColorTooltip { get; set; } = "";
    }

    /// <summary>
    /// Scans all registered events (ModelDb.AllEvents), captures text + options offline, and records
    /// act membership + mod origin. Cached to JSON with an incremental diff on later launches.
    /// Colour classification (green/yellow/red) is computed against the live run state at display time.
    /// </summary>
    internal static class EventCatalog
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private static readonly List<EventInfo> _events = new();
        private static bool _loaded;

        public static IReadOnlyList<EventInfo> Events { get { EnsureLoaded(); return _events; } }

        // Bump when EventInfo shape / capture logic changes; combined with mod version + game version
        // to invalidate stale caches (old builds miss new events / old text).
        private const int SchemaVersion = 8;

        // Mod's own version — keep in sync with mod_manifest.json. Any mod update invalidates the cache.
        private const string ModVersion = "1.0.4";

        // NOTE: extension is NOT .json so the mod loader doesn't try to parse it as a manifest.
        // Each language has its OWN cache file: nodeforge_events.<lang>.cache — so switching the game
        // language always reads that language's independently captured texts.
        private static string CacheDir =>
            Path.GetDirectoryName(typeof(EventCatalog).Assembly.Location) ?? ".";

        public static string CachePathFor(string lang) =>
            Path.Combine(CacheDir, "nodeforge_events." + SanitizeLang(lang) + ".cache");

        public static string CachePath => CachePathFor(_loadedLang == "" ? CurrentLang() : _loadedLang);

        private static string SanitizeLang(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "zhs";
            var sb = new System.Text.StringBuilder();
            foreach (var ch in lang!)
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.Length == 0 ? "zhs" : sb.ToString();
        }

        /// <summary>Language whose cache is currently loaded in memory.</summary>
        private static string _loadedLang = "";

        private sealed class CacheDoc
        {
            public string Version { get; set; } = "";
            public List<EventInfo> Events { get; set; } = new();
        }

        private static string CurrentLang()
        {
            try { return MegaCrit.Sts2.Core.Localization.LocManager.Instance?.Language ?? "zhs"; }
            catch { return "zhs"; }
        }

        private static string VersionTag
        {
            get
            {
                string gv = "?";
                try
                {
                    var gd = Path.GetDirectoryName(typeof(EventCatalog).Assembly.Location);
                    // Look up a few dirs for release_info.json (game install folder).
                    foreach (var cand in GameDirs())
                    {
                        var p = Path.Combine(cand, "release_info.json");
                        if (File.Exists(p))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(p), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                            if (m.Success) { gv = m.Groups[1].Value; break; }
                        }
                    }
                }
                catch { }
                if (gv == "?")
                {
                    try { gv = typeof(MegaCrit.Sts2.Core.Models.EventModel).Assembly.GetName().Version?.ToString() ?? "?"; } catch { }
                }
                return $"v{SchemaVersion}|mod{ModVersion}|game{gv}|lang{(_loadedLang == "" ? CurrentLang() : _loadedLang)}";
            }
        }

        private static IEnumerable<string> GameDirs()
        {
            var seen = new HashSet<string>();
            string? d = Path.GetDirectoryName(typeof(EventCatalog).Assembly.Location);
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(d); i++)
            {
                if (seen.Add(d!)) yield return d!;
                d = Path.GetDirectoryName(d);
            }
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            string lang = CurrentLang();
            LoadLanguage(lang);
            try { Refresh(false); }
            catch (Exception e) { Log.Warn("event refresh failed: " + e.Message); }
            _loadedLang = lang;
            _capturedLang = lang;
        }

        /// <summary>Load only the given language's own cache file (independent per language).</summary>
        private static void LoadLanguage(string lang)
        {
            _events.Clear();
            try
            {
                string path = CachePathFor(lang);
                if (!File.Exists(path))
                {
                    Log.Info($"event cache for '{lang}' not found -> will capture");
                    return;
                }
                var doc = JsonSerializer.Deserialize<CacheDoc>(File.ReadAllText(path), JsonOpts);
                if (doc != null && doc.Events.Count > 0)
                {
                    _events.AddRange(doc.Events);
                    Log.Info($"event cache loaded: lang={lang}, {doc.Events.Count} events");
                }
            }
            catch (Exception e) { Log.Warn("event cache load failed (" + lang + "): " + e.Message); }
        }

        /// <summary>Called when the menu opens. Always reconciles with the live model DB so events that
        /// became available after the (early) initial scan — e.g. mod events merged into Act.AllEvents
        /// after the model DB finished initialising — show up. Incremental & cheap.</summary>
        private static string _capturedLang = "";

        /// <summary>Language the current cached event texts were captured in.</summary>
        public static string CacheLanguage { get { return _capturedLang; } }

        /// <summary>If the game language differs from the loaded cache, SAVE the old language and LOAD the
        /// new language's own cache (capturing it if it does not exist yet). Returns true if switched.</summary>
        public static bool EnsureLanguageFresh()
        {
            EnsureLoaded();
            try
            {
                string lang = CurrentLang();
                if (string.IsNullOrEmpty(_loadedLang) || _loadedLang == lang) return false;

                // Persist what we have under the OLD language file, then switch to the new one.
                try { Save(); } catch { }
                string old = _loadedLang;
                LoadLanguage(lang);
                try { Refresh(false); } catch { }
                _loadedLang = lang;
                _capturedLang = lang;
                Log.Info($"event cache switched: {old} -> {lang}");
                return true;
            }
            catch (Exception e) { Log.Warn("event lang switch: " + e.Message); }
            return false;
        }

        public static void EnsureFresh()
        {
            EnsureLoaded();
            EnsureLanguageFresh();
            try { Refresh(false); }
            catch (Exception e) { Log.Warn("event ensurefresh: " + e.Message); }
        }

        public static void Refresh() => Refresh(false);

        public static void Refresh(bool forceText)
        {
            // Build act membership: eventId -> set of act indices.
            var actMap = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            var shared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var act in ModelDb.Acts)
                {
                    int idx;
                    try { idx = act.Index; } catch { idx = -1; }
                    if (idx < 0) continue;
                    try
                    {
                        foreach (var ev in act.AllEvents)
                        {
                            var id = SafeId(ev);
                            if (id == "?") continue;
                            if (!actMap.TryGetValue(id, out var set)) { set = new HashSet<int>(); actMap[id] = set; }
                            set.Add(idx);
                        }
                        // Ancients are NOT in AllEvents — include them so they get act membership too.
                        foreach (var anc in act.AllAncients)
                        {
                            var id = SafeId(anc);
                            if (id == "?") continue;
                            if (!actMap.TryGetValue(id, out var set2)) { set2 = new HashSet<int>(); actMap[id] = set2; }
                            set2.Add(idx);
                        }
                    }
                    catch (Exception e) { Log.Warn($"act[{idx}].AllEvents: {e.Message}"); }
                }
                try
                {
                    foreach (var ev in ModelDb.AllSharedEvents) shared.Add(SafeId(ev));
                }
                catch (Exception e) { Log.Warn("AllSharedEvents: " + e.Message); }
            }
            catch (Exception e) { Log.Warn("act map build: " + e.Message); }

            // Incremental: for events already known, keep their captured text and only refresh cheap
            // metadata (act membership / shared / mod). Only NEW ids get a full Capture.
            var byId = _events.ToDictionary(x => x.Id, x => x);
            int added = 0;
            var seenLive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ev in SafeAllEvents())
            {
                try
                {
                    var id = SafeId(ev);
                    if (id == "?") continue;
                    seenLive.Add(id);

                    if (byId.TryGetValue(id, out var known) && !string.IsNullOrEmpty(known.Title))
                    {
                        known.IsMissing = false;   // it exists again
                        // Existing entry: update metadata only, skip re-capture — UNLESS the UI language
                        // changed, in which case we re-capture the localized title/description/options.
                        known.ActIndices = actMap.TryGetValue(id, out var a1) ? a1.OrderBy(x => x).ToList() : new List<int>();
                        known.IsShared = shared.Contains(id);
                        DetectMod(ev, known);
                        if (forceText)
                        {
                            try
                            {
                                var fresh = Capture(ev);
                                known.Title = fresh.Title;
                                known.Description = fresh.Description;
                                known.Options = fresh.Options;
                                if (fresh.Relics.Count > 0 || known.Relics.Count == 0) known.Relics = fresh.Relics;
                                fresh.ActIndices = known.ActIndices;
                                fresh.IsShared = known.IsShared;
                                fresh.IsAncient = known.IsAncient;
                                fresh.IsModEvent = known.IsModEvent;
                                fresh.SourceModId = known.SourceModId;
                                fresh.SourceModName = known.SourceModName;
                                byId[id] = fresh;
                            }
                            catch { }
                        }
                        continue;
                    }

                    var info = Capture(ev);
                    if (actMap.TryGetValue(info.Id, out var acts)) info.ActIndices = acts.OrderBy(x => x).ToList();
                    info.IsShared = shared.Contains(info.Id);
                    DetectMod(ev, info);
                    byId[info.Id] = info;
                    added++;
                }
                catch (Exception e) { Log.Warn($"capture {SafeId(ev)} failed: {e.Message}"); }
            }

            if (byId.Count == 0)
            {
                Log.Warn("event refresh produced 0 events; keeping existing cache (" + _events.Count + ").");
                return;
            }

            // Events present in cache but no longer registered (e.g. their mod was removed): keep the
            // captured text but flag them as missing so the UI shows them black at the bottom.
            // Only trust this when the live scan clearly succeeded (enough live events); a tiny/partial
            // scan (model DB not ready) must NOT mark everything missing.
            int missing = 0;
            if (seenLive.Count >= 20)
            {
                foreach (var kv in byId)
                {
                    if (!seenLive.Contains(kv.Key))
                    {
                        kv.Value.IsMissing = true;
                        missing++;
                    }
                }
            }

            _events.Clear();
            _events.AddRange(byId.Values.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase));
            Save();
            Log.Info($"event refresh: {_events.Count} events (added {added}, now-missing {missing}).");
        }

        private static void DetectMod(EventModel ev, EventInfo info)
        {
            try
            {
                var type = ev.GetType();
                // RitsuLib-registered content knows its owner mod id.
                try
                {
                    if (STS2RitsuLib.Content.ModContentRegistry.TryGetOwnerModId(type, out var modId) && !string.IsNullOrEmpty(modId))
                    {
                        info.IsModEvent = true;
                        info.SourceModId = modId;
                        info.SourceModName = ResolveModName(modId);
                        return;
                    }
                }
                catch { }

                // Fallback: any assembly other than the game's sts2 assembly is a mod.
                var asm = type.Assembly;
                var gameAsm = typeof(EventModel).Assembly;
                if (asm != gameAsm)
                {
                    info.IsModEvent = true;
                    info.SourceModId = asm.GetName().Name ?? "";
                    info.SourceModName = ResolveModName(info.SourceModId);
                }
            }
            catch { }
        }

        private static string ResolveModName(string modId)
        {
            try
            {
                if (STS2RitsuLib.Compat.RitsuModManager.TryGetModInfo(modId, out var info, null) && info != null)
                {
                    if (!string.IsNullOrEmpty(info.Name)) return info.Name;
                }
            }
            catch { }
            return string.IsNullOrEmpty(modId) ? "?" : modId;
        }

        /// <summary>Compute the colour class for an event against the live run state.
        /// Green = vanilla & allowed; Yellow = vanilla & not allowed; Orange = mod & allowed;
        /// Red = mod & not allowed (or character-locked / unknown).</summary>
        public static void Classify(EventInfo info, IRunState? runState, EventModel? canonical, int currentAct = -1)
        {
            // Missing = was captured before but no longer registered (e.g. its mod was removed).
            if (info.IsMissing)
            {
                info.ColorClass = EventColorClass.Missing;
                info.ColorTooltip = UiText.T("模组不存在（仅保留文本）", "Mod no longer present (text kept)");
                return;
            }

            // "Already triggered" is NOT a disqualifier (e.g. 佛珠 lets you repeat events). We only use the
            // game's own IsAllowed(runState) gate, plus whether it can appear on the current act.
            bool allowed = true;
            bool hasState = runState != null && canonical != null;
            if (hasState)
            {
                try { allowed = canonical!.IsAllowed(runState!); }
                catch { allowed = false; }
            }

            // Whether the event can appear on the CURRENT act (shared / unknown-act counts as yes).
            bool inCurrentAct = info.IsShared || info.ActIndices.Count == 0 || (currentAct >= 0 && info.ActIndices.Contains(currentAct));

            if (info.IsModEvent)
            {
                info.ColorClass = (!allowed || !inCurrentAct) ? EventColorClass.Red : EventColorClass.Orange;
                var modName = string.IsNullOrEmpty(info.SourceModName) ? info.SourceModId : info.SourceModName;
                string reason = !inCurrentAct ? UiText.T("不属于当前地图", "not on current map")
                              : allowed ? UiText.T("当前可触发", "currently allowed")
                                        : UiText.T("可能角色/条件受限", "possibly character/condition-locked");
                info.ColorTooltip = UiText.T($"模组事件（{modName}），{reason}", $"Mod event ({modName}), {reason}");
                return;
            }

            if (hasState && allowed && inCurrentAct)
            {
                info.ColorClass = EventColorClass.Green;
                info.ColorTooltip = UiText.T("当前可触发", "Allowed");
            }
            else
            {
                info.ColorClass = EventColorClass.Yellow;
                info.ColorTooltip = !inCurrentAct
                    ? UiText.T("不属于当前地图（强制会变随机问号）", "not on current map (forcing becomes random '?')")
                    : UiText.T("当前不允许（条件不满足）", "Not allowed (conditions unmet)");
            }
        }

        private static IEnumerable<EventModel> SafeAllEvents()
        {
            // Enumerate directly from each Act's AllEvents/AllAncients + shared, NOT via ModelDb.AllEvents
            // (that property caches a lazy list which can be frozen BEFORE mod content is registered,
            //  which would hide mod-added events).
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<ActModel> acts;
            try { acts = ModelDb.Acts; }
            catch (Exception e) { Log.Warn("ModelDb.Acts failed: " + e.Message); acts = Array.Empty<ActModel>(); }

            foreach (var act in acts)
            {
                IEnumerable<EventModel> evs = Array.Empty<EventModel>();
                try { evs = act.AllEvents; } catch (Exception e) { Log.Warn("act.AllEvents failed: " + e.Message); }
                foreach (var ev in evs)
                {
                    if (ev == null) continue;
                    string id = SafeId(ev);
                    if (id == "?" || !seen.Add(id)) continue;
                    yield return ev;
                }
                IEnumerable<EventModel> ancs = Array.Empty<EventModel>();
                try { ancs = act.AllAncients; } catch (Exception e) { Log.Warn("act.AllAncients failed: " + e.Message); }
                foreach (var ev in ancs)
                {
                    if (ev == null) continue;
                    string id = SafeId(ev);
                    if (id == "?" || !seen.Add(id)) continue;
                    yield return ev;
                }
            }

            IEnumerable<EventModel> shared;
            try { shared = ModelDb.AllSharedEvents; }
            catch (Exception e) { Log.Warn("AllSharedEvents failed: " + e.Message); shared = Array.Empty<EventModel>(); }
            foreach (var ev in shared)
            {
                if (ev == null) continue;
                string id = SafeId(ev);
                if (id == "?" || !seen.Add(id)) continue;
                yield return ev;
            }

            IEnumerable<EventModel> sharedAnc;
            try { sharedAnc = ModelDb.AllSharedAncients; }
            catch { sharedAnc = Array.Empty<EventModel>(); }
            foreach (var ev in sharedAnc)
            {
                if (ev == null) continue;
                string id = SafeId(ev);
                if (id == "?" || !seen.Add(id)) continue;
                yield return ev;
            }
        }

        private static string SafeId(EventModel ev)
        {
            try { return ev.Id?.Entry ?? "?"; } catch { return "?"; }
        }

        private static EventInfo Capture(EventModel ev)
        {
            var info = new EventInfo { Id = SafeId(ev) };
            bool isAncient = false;
            try { isAncient = ev is AncientEventModel; info.IsAncient = isAncient; } catch { }
            try { info.Title = ev.Title?.GetRawText() ?? ""; } catch { }
            // Ancients: skip InitialDescription (their description loc key often doesn't follow the common
            // format and triggers a 'missing key' warning; ancients only show their relics anyway).
            if (!isAncient)
                try { info.Description = ev.InitialDescription?.GetRawText() ?? ""; } catch { }

            if (isAncient)
            {
                // Ancients don't use the INITIAL.options format; show all their possible relics instead.
                try { CaptureAncientRelics((AncientEventModel)ev, info); }
                catch (Exception e) { info.Error = e.GetType().Name + ": " + e.Message; }
            }
            else
            {
                try
                {
                    foreach (var loc in ev.GameInfoOptions)
                    {
                        var oi = new EventOptionInfo();
                        try { oi.Title = loc?.GetRawText() ?? ""; } catch { }
                        info.Options.Add(oi);
                    }
                }
                catch (Exception e)
                {
                    TryFillOptionsFromLocTable(info);
                    if (!string.IsNullOrEmpty(info.Title) && info.Options.Count == 0)
                        info.Error = e.GetType().Name + ": " + e.Message;
                }
            }

            if (string.IsNullOrEmpty(info.Title)) info.Title = info.Id;
            return info;
        }

        private static void CaptureAncientRelics(AncientEventModel anc, EventInfo info)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var opt in anc.AllPossibleOptions)
            {
                try
                {
                    var relic = opt?.Relic;
                    if (relic == null) continue;
                    string rid = "";
                    try { rid = relic.Id?.Entry ?? ""; } catch { }
                    string name = "";
                    try { name = relic.Title?.GetRawText() ?? ""; } catch { }
                    if (string.IsNullOrEmpty(name)) name = rid;
                    string desc = "";
                    try { desc = relic.DynamicDescription?.GetRawText() ?? ""; } catch { }
                    if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(desc)) continue;
                    if (!seen.Add(rid + "|" + name + "|" + desc)) continue;
                    info.Relics.Add(new RelicInfo { Id = rid, Name = name, Description = desc });
                }
                catch { }
            }
        }

        private static void TryFillOptionsFromLocTable(EventInfo info)
        {
            try
            {
                var lm = MegaCrit.Sts2.Core.Localization.LocManager.Instance;
                if (lm == null) return;
                var table = lm.GetTable("events");
                if (table == null) return;
                var prefix = info.Id + ".pages.INITIAL.options";
                foreach (var key in table.Keys)
                {
                    if (key.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        try
                        {
                            var ls = new MegaCrit.Sts2.Core.Localization.LocString("events", key);
                            info.Options.Add(new EventOptionInfo { Title = ls.GetRawText() ?? "", Description = "" });
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static EventModel? FindCanonical(string id)
        {
            try
            {
                foreach (var ev in ModelDb.AllEvents)
                    if (string.Equals(SafeId(ev), id, StringComparison.OrdinalIgnoreCase)) return ev;
            }
            catch { }
            return null;
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath) ?? ".");
                var doc = new CacheDoc { Version = VersionTag, Events = _events };
                File.WriteAllText(CachePath, JsonSerializer.Serialize(doc, JsonOpts));
            }
            catch (Exception e) { Log.Warn("event cache save failed: " + e.Message); }
        }

        public static EventInfo? Find(string id)
        {
            EnsureLoaded();
            foreach (var e in _events) if (e.Id == id) return e;
            return null;
        }
    }
}



