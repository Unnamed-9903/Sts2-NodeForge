using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using MegaCrit.Sts2.Core.Map;

namespace NodeForge
{
    /// <summary>
    /// Persisted per-mod settings. Stored via RitsuLib ModDataStore under the key "settings"
    /// (see SettingsRegistration) so the RitsuLib settings page can bind directly to it.
    /// Also mirrored to a local JSON file as a fallback so gameplay features keep working
    /// even if the RitsuLib store is unavailable.
    /// </summary>
    public sealed class NodeForgeSettings
    {
        // --- Overlay hotkeys (key names stored as strings, e.g. "F6") ---
        public string Menu1Hotkey { get; set; } = "F6";
        public string Menu2Hotkey { get; set; } = "F7";

        // --- Menu 1: bulk node replacement (original nodeswap feature) ---
        public bool EffectDisabled { get; set; } = false;
        public string TargetNode { get; set; } = "treasure";

        // Source node types to convert. DEFAULT: none (nothing replaced until the player opts in
        // during a session). These switches are NOT persisted — they reset to false each launch.
        [JsonIgnore] public bool ConvertQuestion { get; set; } = false;
        [JsonIgnore] public bool ConvertElite { get; set; } = false;
        [JsonIgnore] public bool ConvertMonster { get; set; } = false;
        [JsonIgnore] public bool ConvertShop { get; set; } = false;
        [JsonIgnore] public bool ConvertRestSite { get; set; } = false;
        [JsonIgnore] public bool ConvertTreasure { get; set; } = false;
        [JsonIgnore] public bool ConvertAncient { get; set; } = false;

        // Ancient variation (fixed: uses RunState.Rng.Seed instead of removed Rng.Seed)
        public bool VaryAncientByFloor { get; set; } = false;

        // --- Menu 2: hover-to-edit config ---
        // Chosen target node type id for the hover editor (never Boss).
        public string HoverTargetNode { get; set; } = "monster";
        // When HoverTargetNode == "event", optionally force a specific event model id ('' = random).
        public string HoverForceEventId { get; set; } = "";

        // --- Event list colour coding (hex strings, configurable in RitsuLib settings) ---
        public string EventColorGreen { get; set; } = "6FD3A8";   // normal / allowed (vanilla)
        public string EventColorYellow { get; set; } = "E8C24B";  // conditions not met (vanilla)
        public string EventColorOrange { get; set; } = "F08A3C";  // mod event that passes IsAllowed
        public string EventColorRed { get; set; } = "E8795E";     // mod event that fails IsAllowed / character-locked
        public string EventColorVisited { get; set; } = "555555"; // already triggered this run (placed at the bottom)

        // Colour of the marker effect drawn on edited map nodes.
        public string MarkRingColor { get; set; } = "FF3838";
        // Whether the marker particle effect is shown on edited nodes.
        public bool MarkerEffectEnabled { get; set; } = true;

        // Pinyin search mode in the event list.
        public PinyinSearchMode PinyinMode { get; set; } = PinyinSearchMode.Both;

        /// <summary>User toggle for pinyin search. Auto-disabled when the game language isn't Chinese.</summary>
        public bool PinyinEnabled { get; set; } = true;

        public enum PinyinSearchMode { Both = 0, Full = 1, Initial = 2 }

        /// <summary>Effective pinyin usage: only when the toggle is on AND the game language is Chinese.</summary>
        [JsonIgnore]
        public bool PinyinActive => PinyinEnabled && IsChinese;

        /// <summary>True when the game language is Chinese (auto-on condition).</summary>
        [JsonIgnore]
        public bool IsChinese
        {
            get
            {
                try
                {
                    var lang = MegaCrit.Sts2.Core.Localization.LocManager.Instance?.Language;
                    return string.IsNullOrEmpty(lang) || lang!.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase);
                }
                catch { return true; }
            }
        }

        [JsonIgnore]
        public MapPointType TargetPointType =>
            NodeTypeCatalog.TryParseConfigId(TargetNode, out var t) ? t : MapPointType.Treasure;

        [JsonIgnore]
        public MapPointType HoverTargetPointType =>
            NodeTypeCatalog.TryParseConfigId(HoverTargetNode, out var t) ? t : MapPointType.Monster;

        public void Normalize()
        {
            TargetNode = NodeTypeCatalog.ToConfigId(TargetPointType);
            HoverTargetNode = NodeTypeCatalog.ToConfigId(HoverTargetPointType);
            if (string.IsNullOrWhiteSpace(Menu1Hotkey)) Menu1Hotkey = "F6";
            if (string.IsNullOrWhiteSpace(Menu2Hotkey)) Menu2Hotkey = "F7";
        }

        public void CopyFrom(NodeForgeSettings o)
        {
            Menu1Hotkey = o.Menu1Hotkey;
            Menu2Hotkey = o.Menu2Hotkey;
            EffectDisabled = o.EffectDisabled;
            TargetNode = o.TargetNode;
            ConvertQuestion = o.ConvertQuestion;
            ConvertElite = o.ConvertElite;
            ConvertMonster = o.ConvertMonster;
            ConvertShop = o.ConvertShop;
            ConvertRestSite = o.ConvertRestSite;
            ConvertTreasure = o.ConvertTreasure;
            ConvertAncient = o.ConvertAncient;
            VaryAncientByFloor = o.VaryAncientByFloor;
            HoverTargetNode = o.HoverTargetNode;
            HoverForceEventId = o.HoverForceEventId;
            EventColorGreen = o.EventColorGreen;
            EventColorYellow = o.EventColorYellow;
            EventColorOrange = o.EventColorOrange;
            EventColorRed = o.EventColorRed;
            EventColorVisited = o.EventColorVisited;
            MarkRingColor = o.MarkRingColor;
            MarkerEffectEnabled = o.MarkerEffectEnabled;
            PinyinMode = o.PinyinMode;
        }

        public string Describe() =>
            $"disabled={EffectDisabled}, target={TargetNode}, hover={HoverTargetNode}, force={(string.IsNullOrEmpty(HoverForceEventId) ? "<random>" : HoverForceEventId)}, m1={Menu1Hotkey}, m2={Menu2Hotkey}";
    }

    /// <summary>Global accessor + lightweight JSON file mirror.</summary>
    internal static class Settings
    {
        private static readonly JsonSerializerOptions ReadOpts = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };
        private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };

        private static NodeForgeSettings _current = new();
        private static STS2RitsuLib.Data.ModDataStore? _store;
        private const string DataKey = "settings";
        private static bool _requiresNormalizeOnRead = true;

        /// <summary>Live settings instance. Prefers the RitsuLib data store so the settings page
        /// and gameplay logic share one object; falls back to a local instance.</summary>
        public static NodeForgeSettings Current
        {
            get
            {
                if (_store != null)
                {
                    try
                    {
                        var s = _store.Get<NodeForgeSettings>(DataKey);
                        if (s != null) { if (_requiresNormalizeOnRead) { s.Normalize(); _requiresNormalizeOnRead = false; } return s; }
                    }
                    catch { }
                }
                return _current;
            }
        }

        /// <summary>Register the data slot (idempotent) and point Current at it.</summary>
        public static void AttachStore(STS2RitsuLib.Data.ModDataStore store)
        {
            try
            {
                store.Register<NodeForgeSettings>(DataKey, "nodeforge_settings", STS2RitsuLib.Utils.Persistence.SaveScope.Global);
            }
            catch { /* already registered */ }
            _store = store;
            try
            {
                var s = store.Get<NodeForgeSettings>(DataKey);
                s.Normalize();
                ResetSessionSwitches(s);
                _current = s;
            }
            catch { }
        }

        /// <summary>Force the Menu-1 conversion switches off (they are session-only, never persisted).</summary>
        public static void ResetSessionSwitches(NodeForgeSettings? s = null)
        {
            s ??= _current;
            s.ConvertQuestion = false;
            s.ConvertElite = false;
            s.ConvertMonster = false;
            s.ConvertShop = false;
            s.ConvertRestSite = false;
            s.ConvertTreasure = false;
            s.ConvertAncient = false;
        }

        public static string FilePath => System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(Settings).Assembly.Location) ?? ".",
            "nodeforge.cfg");

        public static void Load()
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    var s = JsonSerializer.Deserialize<NodeForgeSettings>(System.IO.File.ReadAllText(FilePath), ReadOpts);
                    if (s != null) _current = s;
                }
            }
            catch { _current = new NodeForgeSettings(); }
            _current.Normalize();
            ResetSessionSwitches(_current);
        }

        public static void Set(NodeForgeSettings s)
        {
            s.Normalize();
            var live = Current;
            live.CopyFrom(s);
            live.Normalize();
            if (!ReferenceEquals(live, _current)) _current = live;
            Save();
        }

        public static void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath) ?? ".");
                System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, WriteOpts));
            }
            catch { /* best-effort */ }
        }
    }
}
