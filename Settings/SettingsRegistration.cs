using System;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace NodeForge
{
    internal static class SettingsRegistration
    {
        private const string DataKey = "settings";

        /// <summary>Localized, live-resolved settings text (re-resolves on each display, so it follows
        /// the current game language without needing a restart).</summary>
        private static ModSettingsText Tx(string zh, string en) => ModSettingsText.Dynamic(() => UiText.T(zh, en));

        private static string IntFmt(double d) => ((int)System.Math.Round(d)).ToString();

        private static ModSettingsValueBinding<NodeForgeSettings, double> DoubleBinding(
            Func<NodeForgeSettings, double> get, Action<NodeForgeSettings, double> set)
            => new ModSettingsValueBinding<NodeForgeSettings, double>(ModEntry.ModId, DataKey, SaveScope.Global, get, set);

        public static void Register()
        {
            try
            {
                RitsuLibFramework.RegisterModSettings(ModEntry.ModId, page => page
                    .WithTitle(ModSettingsText.Dynamic(() => UiText.T("节点锻造", "NodeForge")))
                    .WithModDisplayName(ModSettingsText.Literal("NodeForge"))
                    .WithDescription(ModSettingsText.Dynamic(() => UiText.T(
                        "节点锻造的唤出按键与行为设置。",
                        "Hotkeys and behaviour for NodeForge overlays.")))
                    .AddSection("hotkeys", section => section
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("浮层唤出按键", "Overlay hotkeys")))
                        .WithDescription(ModSettingsText.Dynamic(() => UiText.T(
                            "在游戏内（跑局中/主菜单）按下对应按键唤出浮层面板。",
                            "Press these keys in-game to open the overlays.")))
                        .AddKeyBinding(
                            "toggle_menu1",
                            ModSettingsText.Dynamic(() => UiText.T("子菜单1：节点批量替换", "Menu 1: bulk node replace")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.Menu1Hotkey, (s, v) => s.Menu1Hotkey = v))
                        .AddKeyBinding(
                            "toggle_menu2",
                            ModSettingsText.Dynamic(() => UiText.T("子菜单2：悬浮编辑节点", "Menu 2: hover-edit node")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.Menu2Hotkey, (s, v) => s.Menu2Hotkey = v)))
                    .AddSection("eventcolors", section => section
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("事件列表颜色", "Event list colours")))
                        .WithDescription(ModSettingsText.Dynamic(() => UiText.T(
                            "子菜单2 事件列表的四色标注（绿/黄/橙/红）。仅影响显示，不影响能否选择。",
                            "Colour coding for the Menu 2 event list (green/yellow/orange/red). Visual only.")))
                        .AddColor("color_green",
                            ModSettingsText.Dynamic(() => UiText.T("绿色：正常可触发", "Green: normal")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.EventColorGreen, (s, v) => s.EventColorGreen = v))
                        .AddColor("color_yellow",
                            ModSettingsText.Dynamic(() => UiText.T("黄色：条件不满足", "Yellow: conditions not met")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.EventColorYellow, (s, v) => s.EventColorYellow = v))
                        .AddColor("color_orange",
                            ModSettingsText.Dynamic(() => UiText.T("橙色：模组事件（可触发）", "Orange: mod event (allowed)")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.EventColorOrange, (s, v) => s.EventColorOrange = v))
                        .AddColor("color_red",
                            ModSettingsText.Dynamic(() => UiText.T("红色：模组事件（角色/条件受限）", "Red: mod event (locked)")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.EventColorRed, (s, v) => s.EventColorRed = v))
                        .AddColor("color_visited",
                            ModSettingsText.Dynamic(() => UiText.T("已触发过（置底）", "Already triggered (bottom)")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.EventColorVisited, (s, v) => s.EventColorVisited = v)))
                    .AddSection("mark", section => section
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("被改节点特效", "Edited-node effect")))
                        .WithDescription(ModSettingsText.Dynamic(() => UiText.T(
                            "在地图上被修改过的节点周围的下落粒子特效。",
                            "Falling particle effect around edited map nodes.")))
                        .AddToggle("mark_enabled",
                            ModSettingsText.Dynamic(() => UiText.T("启用节点特效", "Enable node effect")),
                            new ModSettingsValueBinding<NodeForgeSettings, bool>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.MarkerEffectEnabled, (s, v) => s.MarkerEffectEnabled = v))
                        .AddColor("mark_color",
                            ModSettingsText.Dynamic(() => UiText.T("标记粒子颜色", "Marker particle colour")),
                            new ModSettingsValueBinding<NodeForgeSettings, string>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.MarkRingColor, (s, v) => s.MarkRingColor = v)))
                    .AddSection("search", section => section
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("搜索", "Search")))
                        .AddToggle("pinyin_enabled",
                            ModSettingsText.Dynamic(() => UiText.T("启用拼音搜索（仅中文语言时生效）", "Enable pinyin search (Chinese language only)")),
                            new ModSettingsValueBinding<NodeForgeSettings, bool>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.PinyinEnabled, (s, v) => s.PinyinEnabled = v))
                        .AddEnumChoice(
                            "pinyin_mode",
                            ModSettingsText.Dynamic(() => UiText.T("拼音搜索模式", "Pinyin search mode")),
                            new ModSettingsValueBinding<NodeForgeSettings, NodeForgeSettings.PinyinSearchMode>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.PinyinMode, (s, v) => s.PinyinMode = v),
                            v => ModSettingsText.Dynamic(() => v switch
                            {
                                NodeForgeSettings.PinyinSearchMode.Full => UiText.T("全拼（luonan）", "Full pinyin"),
                                NodeForgeSettings.PinyinSearchMode.Initial => UiText.T("首字母（lnwp）", "Initials"),
                                _ => UiText.T("两者皆可", "Both"),
                            })))
                    // ===================== Equivalent Exchange / 万象之源 =====================
                    .AddSection("exchange", section => section
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("等价交换（万象之源）", "Equivalent Exchange (EMC)")))
                        .WithEnabledWhen(() => CombatExchange.SettingsEditable)
                        .WithDescription(ModSettingsText.Dynamic(() => UiText.T(
                            "开启后：F7 仅可单点替换节点并消耗“万象之源”；F6 变为献祭键（悬停自己遗物→献祭换取点数）。资源与配置以主机为准。",
                            "When on: F7 edits a single node for EMC points; F6 becomes the sacrifice key (hover your relic -> sacrifice for points). Host is authoritative.")))
                        .AddToggle("exchange_enabled",
                            ModSettingsText.Dynamic(() => UiText.T("开启等价交换模式", "Enable Equivalent Exchange")),
                            new ModSettingsValueBinding<NodeForgeSettings, bool>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.ExchangeEnabled, (s, v) => s.ExchangeEnabled = v))
                        .AddToggle("exchange_cheat",
                            ModSettingsText.Dynamic(() => UiText.T("显示作弊按钮（局内 EMC 旁，主机可用）", "Show cheat button (next to EMC in-run, host only)")),
                            new ModSettingsValueBinding<NodeForgeSettings, bool>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.ExchangeCheatEnabled, (s, v) => s.ExchangeCheatEnabled = v)))
                    .AddSection("exchange_base", section => section
                        .Collapsible(true)
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("初始资源与倍数", "Starting Points & Multiplier")))
                        .WithEnabledWhen(() => CombatExchange.SettingsEditable)
                        .AddSlider("exchange_start",
                            ModSettingsText.Dynamic(() => UiText.T("开局初始点数", "Starting points")),
                            DoubleBinding(s => s.ExchangeStartPoints, (s, v) => s.ExchangeStartPoints = v),
                            0, 50, 1, d => ((int)System.Math.Round(d)).ToString())
                        .AddSlider("exchange_mult",
                            ModSettingsText.Dynamic(() => UiText.T("多人专属倍数（初始与消耗，默认2）", "Multiplayer-only multiplier (start & cost, default 2)")),
                            DoubleBinding(s => s.ExchangeMultiplier, (s, v) => s.ExchangeMultiplier = v),
                            1, 10, 1, d => ((int)System.Math.Round(d)).ToString())
                        .AddSlider("exchange_mult_solo",
                            ModSettingsText.Dynamic(() => UiText.T("单人专属倍数（挑战用，默认1）", "Singleplayer-only multiplier (challenge, default 1)")),
                            DoubleBinding(s => s.ExchangeMultiplierSolo, (s, v) => s.ExchangeMultiplierSolo = v),
                            1, 10, 1, d => ((int)System.Math.Round(d)).ToString())
                        .AddEnumChoice("exchange_mult_mode",
                            ModSettingsText.Dynamic(() => UiText.T("多人倍数模式", "Multiplayer multiplier mode")),
                            new ModSettingsValueBinding<NodeForgeSettings, NodeForgeSettings.ExchangeMultMode>(
                                ModEntry.ModId, DataKey, SaveScope.Global,
                                s => s.ExchangeMultiplierMode, (s, v) => s.ExchangeMultiplierMode = v),
                            v => ModSettingsText.Dynamic(() => v switch
                            {
                                NodeForgeSettings.ExchangeMultMode.PlayerCount => UiText.T("按人数倍", "By player count"),
                                _ => UiText.T("固定倍数", "Fixed"),
                            })))
                    .AddSection("exchange_val", section => section
                        .Collapsible(true)
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("遗物献祭价值（获得点数）", "Relic sacrifice value (points gained)")))
                        .WithEnabledWhen(() => CombatExchange.SettingsEditable)
                        .AddSlider("exv_starter", ModSettingsText.Dynamic(() => UiText.T("初始遗物", "Starter")), DoubleBinding(s => s.ExchangeValStarter, (s, v) => s.ExchangeValStarter = v), 0, 20, 1, IntFmt)
                        .AddSlider("exv_common", ModSettingsText.Dynamic(() => UiText.T("普通", "Common")), DoubleBinding(s => s.ExchangeValCommon, (s, v) => s.ExchangeValCommon = v), 0, 20, 1, IntFmt)
                        .AddSlider("exv_uncommon", ModSettingsText.Dynamic(() => UiText.T("罕见", "Uncommon")), DoubleBinding(s => s.ExchangeValUncommon, (s, v) => s.ExchangeValUncommon = v), 0, 20, 1, IntFmt)
                        .AddSlider("exv_rare", ModSettingsText.Dynamic(() => UiText.T("稀有", "Rare")), DoubleBinding(s => s.ExchangeValRare, (s, v) => s.ExchangeValRare = v), 0, 20, 1, IntFmt)
                        .AddSlider("exv_shop", ModSettingsText.Dynamic(() => UiText.T("商店", "Shop")), DoubleBinding(s => s.ExchangeValShop, (s, v) => s.ExchangeValShop = v), 0, 20, 1, IntFmt)
                        .AddSlider("exv_ancient", ModSettingsText.Dynamic(() => UiText.T("先古", "Ancient")), DoubleBinding(s => s.ExchangeValAncient, (s, v) => s.ExchangeValAncient = v), 0, 20, 1, IntFmt)
                        .AddSlider("exv_event", ModSettingsText.Dynamic(() => UiText.T("事件", "Event")), DoubleBinding(s => s.ExchangeValEvent, (s, v) => s.ExchangeValEvent = v), 0, 20, 1, IntFmt))
                    .AddSection("exchange_cost", section => section
                        .Collapsible(true)
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("节点修改消耗（消耗点数）", "Node-change cost (points spent)")))
                        .WithEnabledWhen(() => CombatExchange.SettingsEditable)
                        .AddSlider("exc_monster", ModSettingsText.Dynamic(() => UiText.T("小怪", "Monster")), DoubleBinding(s => s.ExchangeCostMonster, (s, v) => s.ExchangeCostMonster = v), 0, 30, 1, IntFmt)
                        .AddSlider("exc_elite", ModSettingsText.Dynamic(() => UiText.T("精英", "Elite")), DoubleBinding(s => s.ExchangeCostElite, (s, v) => s.ExchangeCostElite = v), 0, 30, 1, IntFmt)
                        .AddSlider("exc_question", ModSettingsText.Dynamic(() => UiText.T("问号(?)", "Question (?)")), DoubleBinding(s => s.ExchangeCostQuestion, (s, v) => s.ExchangeCostQuestion = v), 0, 30, 1, IntFmt)
                        .AddSlider("exc_shop", ModSettingsText.Dynamic(() => UiText.T("商店", "Shop")), DoubleBinding(s => s.ExchangeCostShop, (s, v) => s.ExchangeCostShop = v), 0, 30, 1, IntFmt)
                        .AddSlider("exc_treasure", ModSettingsText.Dynamic(() => UiText.T("宝箱", "Treasure")), DoubleBinding(s => s.ExchangeCostTreasure, (s, v) => s.ExchangeCostTreasure = v), 0, 30, 1, IntFmt)
                        .AddSlider("exc_rest", ModSettingsText.Dynamic(() => UiText.T("火堆", "Campfire")), DoubleBinding(s => s.ExchangeCostRest, (s, v) => s.ExchangeCostRest = v), 0, 30, 1, IntFmt)
                        .AddSlider("exc_event", ModSettingsText.Dynamic(() => UiText.T("定向事件", "Directed event")), DoubleBinding(s => s.ExchangeCostEvent, (s, v) => s.ExchangeCostEvent = v), 0, 30, 1, IntFmt)
                        .AddSlider("exc_ancient", ModSettingsText.Dynamic(() => UiText.T("定向先古", "Directed ancient")), DoubleBinding(s => s.ExchangeCostAncient, (s, v) => s.ExchangeCostAncient = v), 0, 30, 1, IntFmt))
                    .AddSection("about", section => section
                        .WithTitle(ModSettingsText.Dynamic(() => UiText.T("说明", "About")))
                        .AddParagraph("about_body", ModSettingsText.Dynamic(() => UiText.T(
                            "子菜单1：沿用原 nodeswap 的“把选中来源节点批量替换为目标节点”功能（已修复远古事件对话卡死）。\n" +
                            "子菜单2：在地图上把鼠标悬停的节点改成选定类型；可选“事件”并强制指定具体事件，悬停事件条目可预览事件全貌。",
                            "Menu 1: bulk-replace selected source node types with a target type (original nodeswap, ancient-freeze bug fixed).\n" +
                            "Menu 2: hover a map node to change its type; choose Event and force a specific event; hover an event entry to preview its full text.")))));
            }
            catch (System.Exception e)
            {
                Log.Error("RegisterModSettings failed: " + e);
            }
        }
    }
}








