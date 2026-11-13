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




