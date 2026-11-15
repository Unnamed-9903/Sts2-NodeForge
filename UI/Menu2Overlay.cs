using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Helpers;
using NodeForge.UI;

namespace NodeForge
{
    /// <summary>
    /// Menu 2 overlay: hover a map node and rewrite its type (or force a specific event / ancient).
    /// Event list is colour-coded (green/yellow/orange/red), grouped & sorted, with a current-map view
    /// and an "all events" panel-internal view. Includes undo and single-map bulk conversion.
    /// </summary>
    internal sealed class Menu2Overlay : CanvasLayer
    {
        public static Menu2Overlay? Instance { get; private set; }

        private bool _built;
        private ColorRect _backdrop = null!;
        private PanelContainer _panel = null!;
        private CenterContainer _center = null!;
        private Label _hoverInfo = null!;
        private Label _errorLabel = null!;
        private OptionButton _target = null!;
        private Label _eventHeader = null!;
        private ScrollContainer _eventScroll = null!;
        private VBoxContainer _eventList = null!;
        private RichTextLabel _preview = null!;
        private HFlowContainer _relicRow = null!;
        private ScrollContainer _relicScroll = null!;
        private PanelContainer _relicTip = null!;
        private Control _tipLayer = null!;
        private HBoxContainer _resourceRow = null!;
        private TextureRect _resourceIcon = null!;
        private Label _resourceLabel = null!;
        private Button _cheatBtn = null!;
        private RichTextLabel _relicTipLabel = null!;
        private LineEdit _search = null!;
        private Label _status = null!;
        private Control _eventSection = null!;
        private OptionButton _actFilter = null!;
        private Button _undoBtn = null!;
        private OptionButton _bulkSource = null!;
        private Button _bulkBtn = null!;

        private string _selectedEventId = "";
        private MapPoint? _lockedPoint;   // node captured when the panel opens (frozen while open)
        private int _actSel = -1; // map filter: -1 = all, >=0 = act index
        private string _filter = "";

        private static string T(string zh, string en) => UiText.T(zh, en);

        public static void Toggle(NGame game)
        {
            EnsureAttached(game);
            if (Instance == null) return;
            if (Instance._center.Visible) Instance.HidePanel();
            else Instance.TryShow();
        }

        public static bool CloseIfOpen()
        {
            if (Instance?._center is { Visible: true }) { Instance.HidePanel(); return true; }
            return false;
        }

        public static void EnsureAttached(NGame game)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                if (Instance.GetParent() == null) game.AddChild(Instance);
                return;
            }
            Instance = new Menu2Overlay { Name = "NodeForgeMenu2" };
            Instance.Init();
            game.AddChild(Instance);
        }

        private void Init()
        {
            Layer = 102;
            ProcessMode = ProcessModeEnum.Always;
            if (!_built) { _built = true; _builtLang = CurLang(); Build(); }
        }

        private static string CurLang()
        {
            try { return MegaCrit.Sts2.Core.Localization.LocManager.Instance?.Language ?? "zhs"; }
            catch { return "zhs"; }
        }

        private static Texture2D? _chipIcon;
        private static Texture2D? TryGetChipIcon()
        {
            if (_chipIcon != null) return _chipIcon;
            try
            {
                foreach (var r in MegaCrit.Sts2.Core.Models.ModelDb.AllRelics)
                {
                    if (r is MegaCrit.Sts2.Core.Models.Relics.GamblingChip)
                    { _chipIcon = r.Icon; break; }
                }
            }
            catch { }
            return _chipIcon;
        }

        private void RefreshResource()
        {
            try
            {
                if (_resourceRow == null) return;
                bool on = CombatExchange.Active;
                _resourceRow.Visible = on;
                if (on && _resourceLabel != null)
                    _resourceLabel.Text = UiText.T($"万象之源：{CombatExchange.DisplayPoints:0}", $"EMC: {CombatExchange.DisplayPoints:0}");
                if (_cheatBtn != null)
                {
                    bool show = on && Settings.Current.ExchangeCheatEnabled;
                    _cheatBtn.Visible = show;
                    _cheatBtn.Disabled = !CombatExchange.IsAuthority; // host only
                    _cheatBtn.TooltipText = CombatExchange.IsAuthority ? "" : UiText.T("仅主机可用", "Host only");
                }
            }
            catch { }
        }

        /// <summary>Called when the exchange pool value changes (local or synced) to refresh the display.</summary>
        public static void NotifyPoolChanged()
        {
            try { if (Instance != null && GodotObject.IsInstanceValid(Instance)) Instance.RefreshResource(); } catch { }
        }

        private void OpenCheatInput()
        {
            try
            {
                if (!CombatExchange.IsAuthority) { SyncToast.Show(UiText.T("只有主机可以增加 EMC。", "Only the host may add EMC.")); return; }
                var dlg = new CanvasLayer { Layer = 241, Name = "NodeForgeCheatInput" };
                AddChild(dlg);
                var backdrop = new ColorRect { Color = new Color(0,0,0,0.55f) };
                backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                backdrop.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) dlg.QueueFree(); };
                dlg.AddChild(backdrop);
                var center = new CenterContainer(); center.SetAnchorsPreset(Control.LayoutPreset.FullRect); center.MouseFilter = Control.MouseFilterEnum.Ignore; dlg.AddChild(center);
                var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(360,0) };
                var sb = new StyleBoxFlat { BgColor = new Color("10242A"), BorderColor = UiTheme.Accent, BorderWidthBottom=2,BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2, CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8, ContentMarginLeft=16,ContentMarginRight=16,ContentMarginTop=14,ContentMarginBottom=14 };
                panel.AddThemeStyleboxOverride("panel", sb);
                center.AddChild(panel);
                var v = new VBoxContainer(); panel.AddChild(v);
                var lbl = new Label { Text = UiText.T("输入要增加的万象之源数量：", "Enter EMC amount to add:") };
                lbl.AddThemeFontSizeOverride("font_size", 15); v.AddChild(lbl);
                var edit = new LineEdit { Text = "10" };
                edit.AddThemeFontSizeOverride("font_size", 16); v.AddChild(edit);
                var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
                row.AddThemeConstantOverride("separation", 14); v.AddChild(row);
                var ok = UiTheme.MakeButton(UiText.T("增加", "Add"), 15); ok.CustomMinimumSize = new Vector2(120,38);
                ok.Pressed += () => {
                    if (double.TryParse(edit.Text, out var n))
                    {
                        if (n > 0) CombatExchange.AddPoints(n);
                    }
                    dlg.QueueFree();
                };
                row.AddChild(ok);
                var cancel = UiTheme.MakeButton(UiText.T("取消", "Cancel"), 15); cancel.CustomMinimumSize = new Vector2(100,38);
                cancel.Pressed += () => dlg.QueueFree(); row.AddChild(cancel);
            }
            catch (Exception e) { Log.Warn("cheat input: " + e.Message); }
        }

        /// <summary>Rebuild the target dropdown labels, appending the EMC cost in exchange mode.</summary>
        private void RefreshTargetLabels()
        {
            try
            {
                if (_target == null) return;
                int prev = _target.Selected;
                bool on = CombatExchange.Active;
                _target.Clear();
                foreach (var o in NodeTypeCatalog.Hover)
                {
                    string label = o.Label;
                    if (on)
                    {
                        double cost = CombatExchange.CostForTarget(o.ConfigId);
                        label += UiText.T($" （{cost:0} 点）", $" ({cost:0} EMC)");
                    }
                    _target.AddItem(label);
                }
                if (prev >= 0 && prev < _target.ItemCount) _target.Selected = prev;
            }
            catch { }
        }
        private string _builtLang = "";

        /// <summary>If the game language changed since the UI was built, tear down and rebuild it so all
        /// labels reflect the current language (in-game live language switching).</summary>
        private void EnsureLanguageRebuilt()
        {
            try
            {
                string lang = CurLang();
                if (_built && _builtLang != lang)
                {
                    _builtLang = lang;
                    foreach (var c in GetChildren()) c.QueueFree();
                    _built = false;
                    Build();
                    _built = true;
                }
            }
            catch (Exception e) { Log.Warn("rebuild ui (lang): " + e.Message); }
        }

        // The game's hover-tip container may sit on a lower CanvasLayer than our panel (Layer=102),
        // which makes relic hover tips render *behind* the panel. Temporarily lift it while open.
        private CanvasLayer? _hoverLayer;
        private int _hoverLayerOriginal = -1;

        private void RaiseHoverTipsAbove()
        {
            try
            {
                var c = MegaCrit.Sts2.Core.Nodes.NGame.Instance?.HoverTipsContainer;
                var n = c;
                while (n != null && n is not CanvasLayer) n = n.GetParent();
                if (n is CanvasLayer cl && cl != this)
                {
                    _hoverLayer = cl;
                    _hoverLayerOriginal = cl.Layer;
                    if (cl.Layer < 200) cl.Layer = 200;   // above our 101/102 panels
                }
            }
            catch (Exception e) { Log.Warn("raise hover tips: " + e.Message); }
        }

        private void RestoreHoverTipsLayer()
        {
            try
            {
                if (_hoverLayer != null && GodotObject.IsInstanceValid(_hoverLayer) && _hoverLayerOriginal >= 0)
                    _hoverLayer.Layer = _hoverLayerOriginal;
            }
            catch { }
            _hoverLayer = null; _hoverLayerOriginal = -1;
        }

        private IRunState? RunState()
        {
            try { return RunManager.Instance?.DebugOnlyGetState(); } catch { return null; }
        }
        private ActMap? CurrentMap()
        {
            try { return RunState()?.Map; } catch { return null; }
        }
        private int CurrentActIndex()
        {
            try { return RunState()?.CurrentActIndex ?? -1; } catch { return -1; }
        }

        private void Build()
        {
            _backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.72f), Visible = false };
            _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _backdrop.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) HidePanel(); };
            AddChild(_backdrop);

            _center = new CenterContainer { Visible = false };
            _center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _center.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_center);

            _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
            _center.AddChild(_panel);
            _panel.AddThemeStyleboxOverride("panel", UiTheme.Panel());
            _panel.CustomMinimumSize = new Vector2(860f, 640f);

            // Our own compact tooltip (drawn inside the panel, above everything in it) for relic hover.
            _relicTip = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 100 };
            var tipStyle = new StyleBoxFlat
            {
                BgColor = new Color("0E1B1E"),
                BorderColor = UiTheme.Accent,
                BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                ContentMarginLeft = 6f, ContentMarginRight = 6f, ContentMarginTop = 4f, ContentMarginBottom = 4f,
            };
            _relicTip.AddThemeStyleboxOverride("panel", tipStyle);
            _relicTipLabel = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _relicTipLabel.AddThemeFontSizeOverride("normal_font_size", 13);
            _relicTipLabel.AddThemeColorOverride("default_color", UiTheme.Text);
            _relicTip.AddChild(_relicTipLabel);
            // Put the tooltip in its own full-rect, mouse-transparent layer ON TOP of everything, so it is
            // NOT stretched by the PanelContainer layout (that was making it fill the whole panel).
            _tipLayer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _tipLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_tipLayer);
            _tipLayer.AddChild(_relicTip);

            var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _panel.AddChild(root);

            var title = new Label { Text = T("悬浮编辑地图节点", "Hover Map Node Editor"), HorizontalAlignment = HorizontalAlignment.Center };
            UiTheme.StyleLabel(title, 24, UiTheme.Accent);
            root.AddChild(title);

            var hint = new Label
            {
                Text = T("鼠标悬停在节点上 → 选择要改成哪种节点 → 应用。Boss 与起点先古之民不可修改。",
                         "Hover a node, pick a type, Apply. Boss and the starting ancient node cannot be edited."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            UiTheme.StyleLabel(hint, 13, UiTheme.TextDim);
            root.AddChild(hint);

            _hoverInfo = new Label { Text = T("当前悬浮：无", "Hovered: none") };
            UiTheme.StyleLabel(_hoverInfo, 16, UiTheme.Text);
            root.AddChild(_hoverInfo);

            // 万象之源 resource row (only shown in Equivalent-Exchange mode).
            _resourceRow = new HBoxContainer { Visible = false };
            _resourceRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(_resourceRow);
            _resourceIcon = new TextureRect
            {
                CustomMinimumSize = new Vector2(28f, 28f),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Texture = TryGetChipIcon(),
            };
            _resourceRow.AddChild(_resourceIcon);
            _resourceLabel = new Label { Text = "" };
            UiTheme.StyleLabel(_resourceLabel, 17, UiTheme.Accent);
            _resourceRow.AddChild(_resourceLabel);
            _cheatBtn = UiTheme.MakeButton(T("作弊", "Cheat"), 13);
            _cheatBtn.CustomMinimumSize = new Vector2(72f, 28f);
            _cheatBtn.Pressed += OpenCheatInput;
            _resourceRow.AddChild(_cheatBtn);

            _errorLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart, Visible = false };
            UiTheme.StyleLabel(_errorLabel, 15, UiTheme.Warn);
            root.AddChild(_errorLabel);

            root.AddChild(new HSeparator());

            var targetRow = new HBoxContainer();
            var tl = new Label { Text = T("改为：", "Change to:"), CustomMinimumSize = new Vector2(80f, 0f) };
            UiTheme.StyleLabel(tl, 17, UiTheme.Text);
            targetRow.AddChild(tl);
            _target = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _target.AddThemeFontSizeOverride("font_size", 16);
            foreach (var o in NodeTypeCatalog.Hover) _target.AddItem(o.Label);
            _target.ItemSelected += _ => OnTargetChanged();
            targetRow.AddChild(_target);            root.AddChild(targetRow);

            // ---- Event section ----
            _eventSection = new VBoxContainer();

            var headerRow = new HBoxContainer();
            _eventHeader = new Label { Text = T("事件", "Events"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            UiTheme.StyleLabel(_eventHeader, 15, UiTheme.Accent);
            headerRow.AddChild(_eventHeader);
            _actFilter = new OptionButton { CustomMinimumSize = new Vector2(170f, 30f) };
            _actFilter.AddThemeFontSizeOverride("font_size", 13);
            _actFilter.ItemSelected += _ => { OnActFilterChanged(); };
            headerRow.AddChild(_actFilter);
            _eventSection.AddChild(headerRow);

            _search = new LineEdit { PlaceholderText = T("搜索事件…", "Search events…") };
            _search.AddThemeFontSizeOverride("font_size", 14);
            _search.TextChanged += txt => { _filter = txt ?? ""; PopulateEventList(); };
            _eventSection.AddChild(_search);

            var split = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _eventScroll = new ScrollContainer { CustomMinimumSize = new Vector2(320f, 300f), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _eventList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _eventScroll.AddChild(_eventList);
            split.AddChild(_eventScroll);

            var rightCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            _preview = new RichTextLabel
            {
                CustomMinimumSize = new Vector2(450f, 150f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                BbcodeEnabled = true,
                FitContent = false,
                ScrollActive = true,
            };
            _preview.AddThemeColorOverride("default_color", UiTheme.Text);
            rightCol.AddChild(_preview);

            _relicScroll = new ScrollContainer { CustomMinimumSize = new Vector2(450f, 170f), SizeFlagsVertical = Control.SizeFlags.ShrinkEnd, Visible = false };
            _relicRow = new HFlowContainer { CustomMinimumSize = new Vector2(440f, 160f), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _relicScroll.AddChild(_relicRow);
            rightCol.AddChild(_relicScroll);
            split.AddChild(rightCol);
            _eventSection.AddChild(split);
            root.AddChild(_eventSection);

            // ---- Bulk (single map) row ----
            var bulkRow = new HBoxContainer();
            var bl = new Label { Text = T("单图批量：把本图所有", "Bulk (this map): all"), };
            UiTheme.StyleLabel(bl, 14, UiTheme.TextDim);
            bulkRow.AddChild(bl);
            _bulkSource = new OptionButton();
            _bulkSource.AddThemeFontSizeOverride("font_size", 14);
            foreach (var o in NodeTypeCatalog.All) if (o.PointType != MapPointType.Boss) _bulkSource.AddItem(o.Label);
            bulkRow.AddChild(_bulkSource);
            var bl2 = new Label { Text = T("改为上方目标", "→ target above") };
            UiTheme.StyleLabel(bl2, 14, UiTheme.TextDim);
            bulkRow.AddChild(bl2);
            _bulkBtn = UiTheme.MakeButton(T("执行", "Run"), 13);
            _bulkBtn.CustomMinimumSize = new Vector2(80f, 30f);
            _bulkBtn.Pressed += BulkCurrentMap;
            bulkRow.AddChild(_bulkBtn);
            root.AddChild(bulkRow);

            _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            UiTheme.StyleLabel(_status, 13, UiTheme.Ok);
            root.AddChild(_status);

            root.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

            var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            _undoBtn = UiTheme.MakeButton(T("撤销上一次", "Undo last"), 14);
            _undoBtn.CustomMinimumSize = new Vector2(150f, 36f);
            _undoBtn.Pressed += UndoLast;
            var apply = UiTheme.MakeButton(T("应用到悬浮节点", "Apply"), 15);
            apply.CustomMinimumSize = new Vector2(180f, 36f);
            apply.Pressed += ApplyToHovered;
            var close = UiTheme.MakeButton(T("关闭", "Close"), 14);
            close.Pressed += HidePanel;
            actions.AddChild(_undoBtn); actions.AddChild(apply); actions.AddChild(close);
            root.AddChild(actions);

            PopulateActFilter(CurrentActIndex());
            UpdateUndoButton();
            OnTargetChanged();
        }

        /// <summary>Rebuild the map (act) filter dropdown: "全部" + each act index found in events.</summary>
        private void PopulateActFilter(int defaultAct)
        {
            _actFilter.Clear();
            _actFilter.AddItem(T("全部地图", "All maps"));
            _actFilter.SetItemMetadata(0, -1);
            int curAct = CurrentActIndex();
            var acts = new SortedSet<int>();
            try
            {
                foreach (var e in EventCatalog.Events)
                    foreach (var a in e.ActIndices) acts.Add(a);
                if (curAct >= 0) acts.Add(curAct);
                for (int i = 0; i < ModelDb.ActsByIndex.Count; i++) acts.Add(i);
            }
            catch { }
            int selIdx = 0;
            int i0 = 1;
            foreach (var a in acts)
            {
                string label = ActTitle(a) + "  (Act " + (a + 1) + ")";
                _actFilter.AddItem(label);
                _actFilter.SetItemMetadata(i0, a);
                if (a == defaultAct) selIdx = i0;
                i0++;
            }
            _actFilter.Selected = selIdx;
            _actSel = selIdx == 0 ? -1 : (int)_actFilter.GetItemMetadata(selIdx).AsInt64();
        }

        private void OnActFilterChanged()
        {
            if (_actFilter.Selected < 0) { _actSel = -1; PopulateEventList(); return; }
            var md = _actFilter.GetItemMetadata(_actFilter.Selected);
            _actSel = md.VariantType == Variant.Type.Nil ? -1 : (int)md.AsInt64();
            PopulateEventList();
        }

        private void UpdateUndoButton()
        {
            if (_undoBtn != null) _undoBtn.Disabled = _lastWasBulk ? !BulkHistory.CanUndo : !EditHistory.CanUndo;
        }

        private static string ActTitle(int index)
        {
            try
            {
                var byIndex = ModelDb.ActsByIndex;
                if (index >= 0 && index < byIndex.Count && byIndex[index].Count > 0)
                    return byIndex[index][0].Title?.GetRawText() ?? ("Act " + (index + 1));
            }
            catch { }
            return "Act " + (index + 1);
        }

        private void PopulateEventList()
        {
            foreach (var child in _eventList.GetChildren()) child.QueueFree();
            var runState = RunState();

            EventCatalog.EnsureFresh();   // rescan if the init-time scan was empty
            var all = EventCatalog.Events.ToList();
            int curActForClassify = _actSel >= 0 ? _actSel : CurrentActIndex();
            foreach (var ev in all)
            {
                var canonical = EventCatalog.FindCanonical(ev.Id);
                EventCatalog.Classify(ev, runState, canonical, curActForClassify);
            }

            // Filter by force kind: directed event -> only normal events; directed ancient -> only ancients.
            var kind = SelectedForce;
            IEnumerable<EventInfo> display;
            if (kind == ForceKind.Ancient)
                display = all.Where(e => e.IsAncient && !e.IsMissing);
            else if (kind == ForceKind.Event)
                display = all.Where(e => !e.IsAncient);
            else
                display = all;
            // Missing events (mod removed) are always shown (black, bottom), regardless of act filter.

            // Map filter: events are act-gated; ANCIENTS are shown for ALL acts; missing always shown.
            if (_actSel >= 0 && kind != ForceKind.Ancient)
                display = display.Where(e => e.IsMissing || e.IsShared || e.ActIndices.Count == 0 || e.ActIndices.Contains(_actSel));

            if (!string.IsNullOrWhiteSpace(_filter))
            {
                var f = _filter.Trim();
                var cfg = Settings.Current;
                // Pinyin is only used when the toggle is on AND the game language is Chinese; otherwise
                // fall back to plain (case-insensitive) substring match on title/id.
                if (cfg.PinyinActive)
                {
                    var pmode = cfg.PinyinMode;
                    display = display.Where(e => Pinyin.Matches(e.Title, e.Id, f, pmode));
                }
                else
                {
                    display = display.Where(e =>
                        (e.Title ?? "").Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        (e.Id ?? "").Contains(f, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Sort: green(0) < yellow(1) < orange(2) < red(3); each by title then id.
            var list = display
                .OrderBy(e => (int)e.ColorClass)
                .ThenBy(e => e.Title ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count == 0)
            {
                // Robust fallback: if the filtered view is empty, show everything of this kind and hint.
                var fallback = (kind == ForceKind.Ancient) ? all.Where(e => e.IsAncient)
                              : (kind == ForceKind.Event) ? all.Where(e => !e.IsAncient)
                              : all;
                var fbList = fallback.OrderBy(e => (int)e.ColorClass).ThenBy(e => e.Title ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                if (fbList.Count == 0)
                {
                    var l = new Label { Text = T("（未扫描到事件）", "(no events scanned)") };
                    UiTheme.StyleLabel(l, 13, UiTheme.TextDim);
                    _eventList.AddChild(l);
                    return;
                }
                var hint = new Label { Text = T("当前地图筛选为空，已显示全部：", "Current-map filter empty, showing all:"), AutowrapMode = TextServer.AutowrapMode.WordSmart };
                UiTheme.StyleLabel(hint, 12, UiTheme.TextDim);
                _eventList.AddChild(hint);
                list = fbList;
            }

            bool ancientMode = kind == ForceKind.Ancient;

            if (!ancientMode)
            {
                var legend = new Label
                {
                    Text = T("绿=允许触发  橙=模组(允许)  黄=不允许/非本图  红=模组(受限)  黑=模组不存在",
                             "G=allowed  O=mod(ok)  Y=not allowed  R=mod(locked)  dark=mod gone"),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                };
                UiTheme.StyleLabel(legend, 12, UiTheme.TextDim);
                _eventList.AddChild(legend);
            }

            int lastClass = -1;
            foreach (var ev in list)
            {
                int cls = ancientMode ? 0 : (int)ev.ColorClass;
                if (!ancientMode && cls != lastClass) { _eventList.AddChild(new HSeparator()); lastClass = cls; }

                // Ancients: no colour coding (just the standard text colour).
                var color = ancientMode ? UiTheme.Text : UiTheme.EventColor(ev.ColorClass);
                bool selected = ev.Id == _selectedEventId;
                var row = new Button
                {
                    Text = (ev.IsAncient ? "★ " : "") + (string.IsNullOrEmpty(ev.Title) ? ev.Id : ev.Title),
                    Alignment = HorizontalAlignment.Left,
                    CustomMinimumSize = new Vector2(0f, 30f),
                    TooltipText = BuildTooltip(ev),
                };
                row.AddThemeFontSizeOverride("font_size", 14);

                // Unselected: left colour bar. Selected: full coloured border box + tinted background.
                var normalStyle = UiTheme.Item(false);
                normalStyle.BorderColor = color;
                if (selected)
                {
                    normalStyle.BorderWidthLeft = normalStyle.BorderWidthTop = normalStyle.BorderWidthRight = normalStyle.BorderWidthBottom = 2;
                    normalStyle.BgColor = new Color(color.R, color.G, color.B, 0.18f);
                }
                else
                {
                    normalStyle.BorderWidthLeft = 5;
                    normalStyle.BorderWidthTop = normalStyle.BorderWidthBottom = normalStyle.BorderWidthRight = 0;
                    normalStyle.CornerRadiusTopLeft = 2; normalStyle.CornerRadiusBottomLeft = 2;
                }
                var hoverStyle = UiTheme.Item(true);
                hoverStyle.BorderColor = color;
                hoverStyle.BorderWidthLeft = hoverStyle.BorderWidthTop = hoverStyle.BorderWidthRight = hoverStyle.BorderWidthBottom = 2;
                row.AddThemeStyleboxOverride("normal", normalStyle);
                row.AddThemeStyleboxOverride("hover", hoverStyle);
                row.AddThemeStyleboxOverride("pressed", hoverStyle);

                row.AddThemeColorOverride("font_color", selected ? UiTheme.Highlight(color) : color);
                row.AddThemeColorOverride("font_hover_color", UiTheme.Highlight(color));

                var captured = ev;
                row.MouseEntered += () => ShowPreview(captured);
                row.Pressed += () => { _selectedEventId = captured.Id; Settings.Current.HoverForceEventId = captured.Id; Settings.Save(); PopulateEventList(); ShowPreview(captured); };
                _eventList.AddChild(row);
            }
        }

        private string BuildTooltip(EventInfo ev)
        {
            var parts = new List<string> { ev.Id };
            if (!string.IsNullOrEmpty(ev.ColorTooltip)) parts.Add(ev.ColorTooltip);
            if (ev.IsShared) parts.Add(T("共享事件（所有地图）", "Shared (all acts)"));
            else if (ev.ActIndices.Count > 0)
                parts.Add(T("地图：" + string.Join(", ", ev.ActIndices.Select(ActTitle)),
                            "Acts: " + string.Join(", ", ev.ActIndices.Select(ActTitle))));
            if (ev.IsModEvent && !string.IsNullOrEmpty(ev.SourceModName))
                parts.Add(T("模组：" + ev.SourceModName, "Mod: " + ev.SourceModName));
            return string.Join("\n", parts);
        }

        private void ShowPreview(EventInfo ev)
        {
            // If the game language changed since the cache was built, re-scan and pick the fresh
            // (current-language) entry for the same id, so the preview matches the active language.
            try
            {
                if (EventCatalog.EnsureLanguageFresh())
                {
                    foreach (var e2 in EventCatalog.Events)
                        if (string.Equals(e2.Id, ev.Id, StringComparison.OrdinalIgnoreCase)) { ev = e2; break; }
                }
            }
            catch { }
            var sb = new System.Text.StringBuilder();
            var color = ev.IsAncient ? UiTheme.Accent.ToHtml() : UiTheme.EventColor(ev.ColorClass).ToHtml();
            sb.Append($"[b][color=#{color}]{Escape(ev.Title)}[/color][/b]\n");
            sb.Append($"[color=#9FB4B0]{Escape(ev.Id)}{(ev.IsAncient ? "  (Ancient)" : "")}[/color]\n");
            if (!ev.IsAncient && !string.IsNullOrEmpty(ev.ColorTooltip))
                sb.Append($"[color=#{color}]{Escape(ev.ColorTooltip)}[/color]\n");

            if (ev.IsAncient)
            {
                // Ancients: no extra hints (map-restriction note removed per request).
            }
            else
            {
                // Tip if this event can't actually be forced under the current run conditions.
                if (!EventAllowedNow(ev, out bool unknown))
                {
                    if (!unknown)
                        sb.Append("[color=#E8795E]⚠ " + T("条件不满足：强制进入时会变成随机问号内容，无法真正进入该事件。", "Conditions not met: forcing this will become a random '?' event.") + "[/color]\n");
                }
                // Tip if the event does not belong to the current map (act).
                try
                {
                    int curAct = _actSel >= 0 ? _actSel : CurrentActIndex();
                    bool inCurAct = ev.IsShared || ev.ActIndices.Count == 0 || (curAct >= 0 && ev.ActIndices.Contains(curAct));
                    if (curAct >= 0 && !inCurAct)
                        sb.Append("[color=#E8C24B]⚠ " + T("该事件不属于当前地图（Act " + (curAct + 1) + "），强制后可能变成随机问号内容。", "This event does not belong to the current act (Act " + (curAct + 1) + "); forcing may become a random '?' event.") + "[/color]\n");
                }
                catch { }
            }

            // Compatibility: show the event's own special button colour if it customises it.
            try
            {
                var canon = EventCatalog.FindCanonical(ev.Id);
                if (canon != null)
                {
                    var bc = canon.ButtonColor;
                    if (bc.R != 1f || bc.G != 1f || bc.B != 1f || bc.A != 0.9f)
                        sb.Append($"[color=#{bc.ToHtml()}]" + T("(此事件有特殊按钮颜色)", "(this event has a special button colour)") + "[/color]\n");
                }
            }
            catch { }

            sb.Append('\n');
            if (!string.IsNullOrEmpty(ev.Description))
            {
                // Convert the game's custom rich-text tags (jitter/sine/purple/...) to standard BBCode.
                sb.Append("[bgcolor=#1B3338] ");
                sb.Append(MegaText.ToBbcode(ev.Description));
                sb.Append(" [/bgcolor]\n\n");
            }
            if (ev.IsAncient)
            {
                // Relics are shown as ICON cells in the row below; here we only show an error if capture failed.
                if ((ev.Relics?.Count ?? 0) == 0 && !string.IsNullOrEmpty(ev.Error))
                    sb.Append("[color=#E8795E]").Append(Escape(ev.Error)).Append("[/color]\n");
            }
            else if (ev.Options.Count > 0)
            {
                sb.Append("[b][color=#E8A54B]" + T("选项：", "Options:") + "[/color][/b]\n");
                foreach (var o in ev.Options)
                {
                    sb.Append(" • ");
                    sb.Append(MegaText.ToBbcode(o.Title));
                    sb.Append('\n');
                }
            }
            if (!ev.IsAncient && !string.IsNullOrEmpty(ev.Error)) sb.Append("\n[color=#E8795E]").Append(Escape(ev.Error)).Append("[/color]");
            _preview.Text = sb.ToString();
            PopulateRelicIcons(ev);
        }

        /// <summary>For ancients, place relic ICONS (like the in-game relic bar) below the text.
        /// Uses the relic's own texture (BigIcon/Icon) in a framed cell — no scene dependency.</summary>
        private void PopulateRelicIcons(EventInfo ev)
        {
            try
            {
                if (_relicRow == null || !GodotObject.IsInstanceValid(_relicRow)) { Log.Warn("relic: row not ready"); return; }
                HideRelicTip();
                foreach (var c in _relicRow.GetChildren()) c.QueueFree();
                if (!ev.IsAncient) { if (_relicScroll != null) _relicScroll.Visible = false; return; }

                Log.Info($"relic: ancient {ev.Id} cached relics={(ev.Relics?.Count ?? 0)}");

                // Build a lookup of all relics by ModelId.Entry once.
                var byEntry = new System.Collections.Generic.Dictionary<string, MegaCrit.Sts2.Core.Models.RelicModel>(System.StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var r in MegaCrit.Sts2.Core.Models.ModelDb.AllRelics)
                    {
                        try { var e = r.Id?.Entry; if (!string.IsNullOrEmpty(e) && !byEntry.ContainsKey(e!)) byEntry[e!] = r; } catch { }
                    }
                }
                catch (Exception ex) { Log.Warn("relic: enumerate AllRelics failed: " + ex.Message); }
                Log.Info($"relic: AllRelics lookup size={byEntry.Count}");

                int added = 0, missing = 0;
                foreach (var ri in (ev.Relics ?? new System.Collections.Generic.List<RelicInfo>()))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(ri.Id)) { missing++; continue; }
                        if (!byEntry.TryGetValue(ri.Id, out var relic)) { missing++; Log.Warn($"relic: id not found '{ri.Id}'"); continue; }
                        var entry = MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection.NRelicCollectionEntry.Create(
                            relic, MegaCrit.Sts2.Core.Entities.UI.ModelVisibility.Visible);
                        if (entry == null) { Log.Warn($"relic: Create returned null for {ri.Id}"); continue; }
                        entry.CustomMinimumSize = new Vector2(76f, 76f);
                        // Disable the game's own hover-tip (it renders on a low layer behind our panel);
                        // use our own in-panel tooltip instead.
                        try { entry.MouseFilter = Control.MouseFilterEnum.Pass; entry.FocusMode = Control.FocusModeEnum.None; } catch { }
                        string tipName = ri.Name, tipDesc = ri.Description;
                        entry.MouseEntered += () => ShowRelicTip(entry, tipName, tipDesc);
                        entry.MouseExited += HideRelicTip;
                        _relicRow.AddChild(entry);
                        added++;
                    }
                    catch (Exception ex) { Log.Warn("relic entry: " + ex.Message); }
                }
                if (_relicScroll != null) _relicScroll.Visible = added > 0;
                Log.Info($"relic: ancient {ev.Id} => added={added} missing={missing}, scrollVisible={_relicScroll?.Visible}");
            }
            catch (Exception e) { Log.Warn("populate relic icons: " + e.Message); }
        }

        private void ShowRelicTip(Control anchor, string name, string desc)
        {
            try
            {
                if (_relicTip == null || _relicTipLabel == null) return;
                var sb = new System.Text.StringBuilder();
                sb.Append("[b][color=#E8A54B]").Append(Escape(name)).Append("[/color][/b]");
                if (!string.IsNullOrEmpty(desc)) sb.Append("\n").Append(MegaText.ToBbcode(desc));
                _relicTipLabel.Text = sb.ToString();
                // Compact width: estimate from the longest line, cap 320, floor 80.
                int maxLen = 0;
                foreach (var ln in sb.ToString().Split('\n')) maxLen = Math.Max(maxLen, ln.Length);
                float w = Mathf.Min(Mathf.Max(maxLen * 10f + 16f, 80f), 320f);
                _relicTipLabel.CustomMinimumSize = new Vector2(w, 0f);
                _relicTip.Visible = true;
                _relicTip.ResetSize();

                // Top-LEFT corner of the tip at the mouse cursor (layer-local), clamped to the viewport.
                Vector2 mouse = _tipLayer.GetLocalMousePosition();
                var vp = GetViewport().GetVisibleRect().Size;
                var sz = _relicTip.Size;
                float x = Mathf.Min(Mathf.Max(mouse.X, 2f), vp.X - sz.X - 2f);
                float y = Mathf.Min(Mathf.Max(mouse.Y, 2f), vp.Y - sz.Y - 2f);
                _relicTip.Position = new Vector2(x, y);
            }
            catch (Exception e) { Log.Warn("show relic tip: " + e.Message); }
        }

        private void HideRelicTip()
        {
            try { if (_relicTip != null) _relicTip.Visible = false; } catch { }
        }

        /// <summary>Whether an event can be forced now. unknown=true if we couldn't evaluate.</summary>
        private bool EventAllowedNow(EventInfo ev, out bool unknown)
        {
            unknown = false;
            try
            {
                var rs = RunState();
                var canon = EventCatalog.FindCanonical(ev.Id);
                if (rs == null || canon == null) { unknown = true; return true; }
                // Only the game's own IsAllowed gate (already-triggered is NOT disqualifying).
                return canon.IsAllowed(rs);
            }
            catch { unknown = true; return true; }
        }

        private static string Escape(string s) => s.Replace("[", "[lb]");

        private ForceKind SelectedForce
            => NodeTypeCatalog.TryGetHoverOption(SelectedTargetConfigId, out var o) ? o.Force : ForceKind.None;

        private void OnTargetChanged()
        {
            bool needsForced = SelectedForce != ForceKind.None;
            _eventSection.Visible = needsForced;
            // Ancients don't need the map filter (all ancients are shown).
            if (_actFilter != null) _actFilter.Visible = needsForced && SelectedForce != ForceKind.Ancient;
            if (needsForced)
            {
                _eventHeader.Text = SelectedForce == ForceKind.Ancient
                    ? T("选择一个远古事件（悬停预览，点击选中）", "Pick an ancient (hover to preview, click to select)")
                    : T("选择一个事件（悬停预览，点击选中）", "Pick an event (hover to preview, click to select)");
                PopulateEventList();
            }
        }

        private string SelectedTargetConfigId
        {
            get
            {
                int i = _target.Selected;
                if (i >= 0 && i < NodeTypeCatalog.Hover.Count) return NodeTypeCatalog.Hover[i].ConfigId;
                return "monster";
            }
        }

        public void OnHoverChanged()
        {
            if (!_built || !_center.Visible) return;
            UpdateHoverInfo();
        }

        private void UpdateHoverInfo()
        {
            var pt = _lockedPoint;   // frozen while the panel is open
            if (pt == null)
            {
                _hoverInfo.Text = T("当前悬浮：无（请把鼠标移到地图节点上）", "Hovered: none");
                _hoverInfo.AddThemeColorOverride("font_color", UiTheme.TextDim);
                return;
            }
            var coord = pt.coord;
            bool ok = MapNodeService.MeetsOpenCondition(pt, HoverState.RunState, CurrentMap(), out string why, HoverState.CurrentState);
            _hoverInfo.Text = T(
                $"当前悬浮：({coord.col},{coord.row})  类型={NodeTypeCatalog.ToLabel(pt.PointType)}" + (ok ? "" : "  " + why),
                $"Hovered: ({coord.col},{coord.row})  type={NodeTypeCatalog.ToLabel(pt.PointType)}" + (ok ? "" : "  " + why));
            _hoverInfo.AddThemeColorOverride("font_color", ok ? UiTheme.Text : UiTheme.Warn);
        }

        /// <summary>Open only when hovering a valid (non-boss, non-start-ancient, non-current) node.</summary>
        private void TryShow()
        {
            if (!_built) return;
            EnsureLanguageRebuilt();
            if (!_built) return;
            if (!NetAuth.CanEdit())
            {
                // Clients may open F7 read-only in exchange mode (to see the host's real balance),
                // but cannot apply/bulk. Non-exchange mode: clients still blocked.
                if (!(CombatExchange.Active && NetAuth.IsClient()))
                {
                    ShowError(T("只有主机可以修改地图节点（联机客机不可修改）。", "Only the host may edit map nodes (clients cannot)."));
                    return;
                }
            }
            if (!MapNodeService.MeetsOpenCondition(HoverState.Current, HoverState.RunState, CurrentMap(), out string reason, HoverState.CurrentState))
            {
                ShowError(reason);
                return;
            }

            // Mutual exclusion: close Menu 1 if open.
            try { Menu1Overlay.CloseIfOpen(); } catch { }

            // Lock the hovered node for the whole session of this panel.
            _lockedPoint = HoverState.Current;
            _errorLabel.Visible = false;

            var cfg = Settings.Current;
            int sel = -1;
            for (int i = 0; i < NodeTypeCatalog.Hover.Count; i++)
                if (NodeTypeCatalog.Hover[i].ConfigId == cfg.HoverTargetNode) { sel = i; break; }
            if (sel < 0) sel = 0;
            RefreshTargetLabels();
            _target.Selected = sel;

            // Default bulk source = hovered node's current type.
            try
            {
                if (_lockedPoint != null)
                {
                    var srcList = NodeTypeCatalog.All.Where(a => a.PointType != MapPointType.Boss).ToList();
                    for (int i = 0; i < srcList.Count; i++)
                        if (srcList[i].PointType == _lockedPoint.PointType) { _bulkSource.Selected = i; break; }
                }
            }
            catch { }

            _selectedEventId = cfg.HoverForceEventId ?? "";
            _filter = ""; _search.Text = "";
            PopulateActFilter(CurrentActIndex());
            OnTargetChanged();
            UpdateUndoButton();
            if (_bulkBtn != null)
            {
                bool bulkOk = NetAuth.BatchAllowed() && !CombatExchange.Active; // no bulk in exchange mode
                _bulkBtn.Disabled = !bulkOk;
                _bulkBtn.TooltipText = bulkOk ? "" : (CombatExchange.Active
                    ? T("等价交换模式仅支持单点替换", "Exchange mode: single-node only")
                    : T("需进入游戏（跑局中）后才能批量修改", "Bulk editing is only available during a run"));
            }
            RefreshResource();
            if (CombatExchange.Active && CombatExchange.IsAuthority) { try { NodeForgeNet.BroadcastPool(); } catch { } }
            _backdrop.Visible = true;
            _center.Visible = true;
            if (_tipLayer != null) _tipLayer.Visible = true;
            RaiseHoverTipsAbove();
            UpdateHoverInfo();
            FitPanel();
        }

        private void FitPanel()
        {
            try
            {
                var vp = GetViewport().GetVisibleRect().Size;
                float w = Mathf.Min(860f, vp.X * 0.9f);
                float h = Mathf.Min(660f, vp.Y * 0.9f);
                // CenterContainer handles centering; just constrain size.
                _panel.CustomMinimumSize = new Vector2(w, h);
                _panel.Size = new Vector2(w, h);
            }
            catch { }
        }

        /// <summary>Show a transient red toast even when the panel is closed.</summary>
        private void ShowError(string reason)
        {
            try
            {
                if (_errorToast == null || !GodotObject.IsInstanceValid(_errorToast))
                {
                    _errorToast = new Label
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    _errorToast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
                    _errorToast.Position = new Vector2(-300f, 120f);
                    _errorToast.CustomMinimumSize = new Vector2(600f, 0f);
                    ((Label)_errorToast).AddThemeFontSizeOverride("font_size", 20);
                    ((Label)_errorToast).AddThemeColorOverride("font_color", new Color("FF5555"));
                    AddChild(_errorToast);
                }
                _errorToast.Text = reason;
                _errorToast.Visible = true;
                // Auto-hide after 5 seconds (restart timer on each new message).
                ToastHideTimer(5.0);
            }
            catch { }
            // Also reflect inside the panel if it's open.
            _errorLabel.Text = reason;
            _errorLabel.Visible = true;
        }
        private Label? _errorToast;
        private SceneTreeTimer? _toastTimer;

        private void ToastHideTimer(double seconds)
        {
            try
            {
                _toastTimer?.Disconnect(SceneTreeTimer.SignalName.Timeout, Callable.From(OnToastTimeout));
            }
            catch { }
            try
            {
                _toastTimer = GetTree().CreateTimer(seconds, true, false, false);
                _toastTimer.Timeout += OnToastTimeout;
            }
            catch { }
        }

        private void OnToastTimeout()
        {
            try
            {
                if (_errorToast != null && GodotObject.IsInstanceValid(_errorToast)) _errorToast.Visible = false;
                _errorLabel.Visible = false;
            }
            catch { }
        }

        private void ApplyToHovered()
        {
            if (!NetAuth.CanEdit())
            {
                ShowError(T("只有主机可以修改地图节点。", "Only the host may edit map nodes."));
                return;
            }
            var pt = _lockedPoint;
            string why = "";
            bool ok = pt != null && MapNodeService.MeetsOpenCondition(pt, HoverState.RunState, CurrentMap(), out why, HoverState.CurrentState);
            if (!ok)
            {
                ShowError(string.IsNullOrEmpty(why) ? T("请先悬浮到一个节点。", "Hover a node first.") : why);
                return;
            }

            string cfgId = SelectedTargetConfigId;
            bool needsForced = NodeTypeCatalog.TryGetHoverOption(cfgId, out var opt) && opt.NeedsForced;
            string forceId = needsForced ? _selectedEventId : "";

            // Equivalent-Exchange mode: random ancient is NOT allowed (must be a specific ancient), and the
            // edit costs 万象之源 points.
            double cost = 0;
            if (CombatExchange.Active)
            {
                if (opt != null && opt.Force == ForceKind.Ancient && string.IsNullOrEmpty(forceId))
                {
                    _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                    _status.Text = T("等价交换模式下只能定向到具体远古，不支持随机远古。", "In exchange mode you must pick a specific ancient (no random ancient).");
                    ShowError(T("请选择具体远古。", "Pick a specific ancient."));
                    return;
                }
                cost = CombatExchange.CostForTarget(cfgId);
                if (CombatExchange.Points + 1e-9 < cost)
                {
                    _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                    _status.Text = T($"万象之源不足（需要 {cost:0}，当前 {CombatExchange.Points:0}）。",
                                     $"Not enough EMC (need {cost:0}, have {CombatExchange.Points:0}).");
                    ShowError(T("资源不足。", "Not enough points."));
                    return;
                }
            }

            // Block forcing events whose conditions aren't met (they'd become random '?' content anyway).
            if (needsForced && !string.IsNullOrEmpty(forceId))
            {
                var info = EventCatalog.Find(forceId);
                if (info != null && !EventAllowedNow(info, out bool unknown) && !unknown)
                {
                    _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                    _status.Text = T("该事件当前条件不满足，无法强制进入（进去会是随机问号内容）。请换一个事件。",
                                     "Event conditions not met; cannot force (would become random '?'). Pick another.");
                    ShowError(T("该事件当前条件不满足，无法强制。", "Event conditions not met; cannot force."));
                    return;
                }
                // Ancients: if we can't resolve their relic/option set, forcing could hang the event.
                if (info != null && info.IsAncient && info.Relics.Count == 0)
                {
                    _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                    _status.Text = T("该远古事件无法解析（可能缺少数据），强制可能导致卡死，已取消。",
                                     "This ancient could not be resolved; forcing may freeze the game. Cancelled.");
                    ShowError(T("该远古事件无法强制。", "This ancient cannot be forced."));
                    return;
                }
            }
            try
            {
                // Exchange mode: charge points (host authority) BEFORE the edit; abort if it fails.
                if (CombatExchange.Active && cost > 0)
                {
                    if (!CombatExchange.TrySpend(cost))
                    {
                        _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                        _status.Text = T("资源不足，未修改。", "Not enough points; not changed.");
                        return;
                    }
                }
                // Record undo history (original type/forced) before the command mutates the node.
                bool hadF = ForcedEventRegistry.TryGet(pt!.coord, out var prevF);
                EditHistory.Push(new NodeEditRecord(pt.coord, pt.PointType, hadF, prevF, MapMarks.IsMarked(pt.coord)));

                // Mutation goes through the SAME command path as clients (local command model).
                var sk_ = NodeTypeCatalog.HoverConfigIdToPointType(cfgId, out var fk_);
                byte fb_ = fk_ == ForceKind.Ancient ? (byte)2 : fk_ == ForceKind.Event ? (byte)1 : (byte)0;
                // Pass the chosen id for BOTH event and ancient (empty = random, resolved by SubmitApply for ancients).
                NodeForgeNet.SubmitApply(pt.coord, sk_, fb_, fb_ == 0 ? "" : (forceId ?? ""));
                _lastWasBulk = false;
                var c = Settings.Current;
                c.HoverTargetNode = cfgId; c.HoverForceEventId = forceId ?? ""; Settings.Save();
                RefreshNodeVisual(pt!);
                TrySave();

                string target = needsForced
                    ? T($"定向事件({(string.IsNullOrEmpty(forceId) ? "随机" : forceId)})", $"Directed({(string.IsNullOrEmpty(forceId) ? "random" : forceId)})")
                    : NodeTypeCatalog.ToLabel(pt!.PointType);
                _status.AddThemeColorOverride("font_color", UiTheme.Ok);
                _status.Text = T($"已把 ({pt!.coord.col},{pt.coord.row}) 改为 {target}。",
                                 $"Set ({pt.coord.col},{pt.coord.row}) -> {target}.");
                UpdateUndoButton();
                UpdateHoverInfo();
                RefreshResource();
            }
            catch (Exception e)
            {
                _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                _status.Text = "Error: " + e.Message;
                Log.Warn("apply hover edit: " + e);
            }
        }

        private void UndoLast()
        {
            try
            {
                if (_lastWasBulk)
                {
                    var snap = BulkHistory.Pop();
                    if (snap == null) { _lastWasBulk = false; UpdateUndoButton(); return; }
                    var map = CurrentMap();
                    if (map == null) { UpdateUndoButton(); return; }
                    var prot = MapNodeService.CollectProtected(map, RunState());
                    foreach (var (coord, prev) in snap.Changed)
                    {
                        try
                        {
                            if (prot.Contains(coord)) continue;   // never modify protected nodes
                            var p = map.GetPoint(coord);
                            if (p == null) continue;
                            NodeForgeNet.SubmitRevert(coord, prev, false, null, 0);
                            RefreshNodeVisual(p);
                        }
                        catch { }
                    }
                    try { MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance?.RefreshAllPointVisuals(); } catch { }
                    TrySave();
                    _status.AddThemeColorOverride("font_color", UiTheme.Ok);
                    _status.Text = T($"已撤销整批（还原 {snap.Changed.Count} 个节点）。", $"Undid bulk ({snap.Changed.Count} nodes).");
                    if (!BulkHistory.CanUndo) _lastWasBulk = false;
                    UpdateUndoButton();
                    return;
                }

                var rec = EditHistory.Pop();
                if (rec == null) { UpdateUndoButton(); return; }
                var m = CurrentMap();
                MapPoint? pt = null;
                try { pt = m?.GetPoint(rec.Coord); } catch { }
                if (pt == null) { _status.Text = T("找不到该节点，无法撤销。", "Node not found; cannot undo."); UpdateUndoButton(); return; }

                byte pfb = rec.HadForced ? (byte)1 : (byte)0;
                // Route undo through the same command path (locally applied + broadcast if host).
                NodeForgeNet.SubmitRevert(rec.Coord, rec.PrevType, rec.HadForced, rec.PrevForcedId, pfb);
                if (!rec.WasMarked) RemoveRing(pt);
                RefreshNodeVisual(pt);
                TrySave();
                _status.AddThemeColorOverride("font_color", UiTheme.Ok);
                _status.Text = T($"已撤销 ({rec.Coord.col},{rec.Coord.row})。", $"Undone ({rec.Coord.col},{rec.Coord.row}).");
                UpdateUndoButton();
                UpdateHoverInfo();
            }
            catch (Exception e) { Log.Warn("undo: " + e.Message); UpdateUndoButton(); }
        }

        private void RemoveRing(MapPoint pt)
        {
            try
            {
                var screen = MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance;
                if (screen == null) return;
                var f = typeof(MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen)
                    .GetField("_mapPointDictionary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(screen) is System.Collections.IDictionary dict && dict.Contains(pt.coord))
                {
                    if (dict[pt.coord] is Node node)
                        foreach (var c in node.GetChildren())
                            if (c is TextureRect tr && tr.Name.ToString() == "NodeForgeRedRing") tr.QueueFree();
                }
            }
            catch { }
        }

        private void BulkCurrentMap()
        {
            try
            {
                if (CombatExchange.Active)
                {
                    ShowError(T("等价交换模式仅支持单点替换，禁用批量。", "Exchange mode allows single-node edits only (bulk disabled)."));
                    return;
                }
                if (!NetAuth.CanEdit())
                {
                    ShowError(T("只有主机可以修改地图节点。", "Only the host may edit map nodes."));
                    return;
                }
                int idx = _bulkSource.Selected;
                var sources = NodeTypeCatalog.All.Where(a => a.PointType != MapPointType.Boss).ToList();
                if (idx < 0 || idx >= sources.Count) return;
                var srcType = sources[idx].PointType;

                string cfgId = SelectedTargetConfigId;
                bool needsForced = NodeTypeCatalog.TryGetHoverOption(cfgId, out var opt) && opt.NeedsForced;
                string forceId = needsForced ? _selectedEventId : "";

                var map = CurrentMap();
                var rs = RunState();
                if (map == null) { _status.Text = T("当前不在跑局中。", "Not in a run."); return; }
                if (!NetAuth.BatchAllowed())
                {
                    _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                    _status.Text = T("多人模式下已禁用单图批量（为避免不同步）。", "Bulk is disabled in multiplayer (to avoid desync).");
                    ShowError(T("批量修改需在跑局中进行（进入游戏后）。", "Bulk editing is only available during a run."));
                    return;
                }

                if (needsForced && !string.IsNullOrEmpty(forceId))
                {
                    var info = EventCatalog.Find(forceId);
                    if (info != null && !EventAllowedNow(info, out bool unknown) && !unknown)
                    {
                        _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                        _status.Text = T("选中事件条件不满足，无法强制。请换一个事件。", "Selected event not allowed; cannot force. Pick another.");
                        ShowError(T("该事件当前条件不满足，无法强制。", "Event conditions not met; cannot force."));
                        return;
                    }
                }

                var snap = MapNodeService.BulkApply(map, rs, t => t == srcType, cfgId, forceId, "Menu2Bulk", broadcast: true);
                if (snap.Changed.Count > 0) { BulkHistory.Push(snap); _lastWasBulk = true; }

                try
                {
                    var screen = MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance;
                    screen?.RefreshAllPointVisuals();
                }
                catch { }
                MapMarks.RedrawAll();
                TrySave();
                _status.AddThemeColorOverride("font_color", UiTheme.Ok);
                _status.Text = T($"单图批量完成：{snap.Changed.Count} 个节点。", $"Bulk done: {snap.Changed.Count} nodes.");
                UpdateUndoButton();
            }
            catch (Exception e) { Log.Warn("bulk: " + e.Message); }
        }
        private bool _lastWasBulk;

        private static void TrySave()
        {
            try
            {
                var sm = MegaCrit.Sts2.Core.Saves.SaveManager.Instance;
                if (sm != null) TaskHelper.RunSafely(sm.SaveRun(null));
            }
            catch (Exception e) { Log.Warn("save: " + e.Message); }
        }

        private static void RefreshNodeVisual(MapPoint point)
        {
            try
            {
                MapMarks.MarkEdited(point.coord);
                // Reliable full refresh: rebuild the map nodes so icons (incl. style-mod overrides and Boss
                // spine art) are correct. RefreshVisualsInstantly alone does NOT update the icon texture.
                try { MegaCrit.Sts2.Core.Runs.RunManager.Instance?.InvokeMapChanged(false); }
                catch { }
            }
            catch (Exception e) { Log.Warn("refresh node visual: " + e.Message); }
        }

        /// <summary>Refresh a map-point node's icon/state. Calls UpdateIcon (the real texture setter) and
        /// related refresh methods by name, so it also works when style mods have wrapped the icon logic.
        /// Falls back to a deferred re-call on the next frame.</summary>
        internal static void RefreshNodeObject(Godot.Node? node)
        {
            try
            {
                if (node == null) return;
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var t = node.GetType();
                t.GetMethod("UpdateIcon", F)?.Invoke(node, null);
                t.GetMethod("RefreshState", F)?.Invoke(node, null);
                t.GetMethod("RefreshColorInstantly", F)?.Invoke(node, null);
            }
            catch (Exception e) { Log.Warn("refresh node object: " + e.Message); }
        }

        private void HidePanel()
        {
            if (!_built) return;
            _backdrop.Visible = false;
            _center.Visible = false;
            HideRelicTip();
            if (_tipLayer != null) _tipLayer.Visible = false;
            RestoreHoverTipsLayer();
            _lockedPoint = null;
            GetViewport()?.GuiReleaseFocus();
        }
    }
}






