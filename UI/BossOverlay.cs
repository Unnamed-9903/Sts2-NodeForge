using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using NodeForge.UI;

namespace NodeForge
{
    /// <summary>Separate overlay to view/change the current act's boss (main or second). Distinct from Menus 1/2.
    /// Host-authoritative: clients may open read-only but cannot apply. Uses official MapCmd.SetBossEncounter.</summary>
    internal sealed class BossOverlay : CanvasLayer
    {
        public static BossOverlay? Instance;

        private bool _built;
        private Control _root = null!;
        private ColorRect _backdrop = null!;
        private PanelContainer _panel = null!;
        private Label _header = null!;
        private OptionButton _which = null!;          // main / second selector
        private ScrollContainer _list = null!;
        private VBoxContainer _listBox = null!;
        private CheckBox _syncDom = null!;
        private Label _status = null!;
        private string _selectedId = "";
        private readonly List<string> _optionIds = new();

        public static void Toggle(NGame game)
        {
            EnsureAttached(game);
            if (Instance == null) return;
            if (Instance._root.Visible) Instance.HidePanel(); else Instance.TryShow();
        }
        public static bool CloseIfOpen()
        {
            if (Instance?._root is { Visible: true }) { Instance.HidePanel(); return true; }
            return false;
        }
        public static void EnsureAttached(NGame game)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance)) { if (Instance.GetParent() == null) game.AddChild(Instance); return; }
            Instance = new BossOverlay { Name = "NodeForgeBoss" };
            Instance.Init();
            game.AddChild(Instance);
        }

        private void Init()
        {
            Layer = 103;
            ProcessMode = ProcessModeEnum.Always;
            if (!_built) { _built = true; Build(); }
        }

        private void Build()
        {
            _root = new Control { Visible = false };
            _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_root);

            _backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.72f) };
            _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _backdrop.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) HidePanel(); };
            _root.AddChild(_backdrop);

            var center = new CenterContainer();
            center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            center.MouseFilter = Control.MouseFilterEnum.Ignore;
            _root.AddChild(center);

            _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(720f, 560f) };
            _panel.AddThemeStyleboxOverride("panel", UiTheme.Panel());
            center.AddChild(_panel);

            var v = new VBoxContainer();
            _panel.AddChild(v);

            var title = new Label { Text = UiText.T("修改 Boss", "Change Boss"), HorizontalAlignment = HorizontalAlignment.Center };
            UiTheme.StyleLabel(title, 22, UiTheme.Accent);
            v.AddChild(title);

            // Which-boss selector (clearly names the target, esp. for dual-boss acts).
            var whichRow = new HBoxContainer();
            var wl = new Label { Text = UiText.T("编辑对象：", "Editing:") };
            UiTheme.StyleLabel(wl, 15, UiTheme.Text);
            whichRow.AddChild(wl);
            _which = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _which.AddThemeFontSizeOverride("font_size", 15);
            _which.ItemSelected += _ => RebuildList();
            whichRow.AddChild(_which);
            v.AddChild(whichRow);

            _header = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            UiTheme.StyleLabel(_header, 13, UiTheme.TextDim);
            v.AddChild(_header);

            _list = new ScrollContainer { CustomMinimumSize = new Vector2(680f, 320f), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            v.AddChild(_list);
            _listBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _listBox.AddThemeConstantOverride("separation", 6);
            _list.AddChild(_listBox);

            _syncDom = new CheckBox { Text = UiText.T("同时切换此 Boss 的噩梦/领域加成（Dread Dominion）", "Also switch this boss's nightmare buff (Dominion)") };
            _syncDom.AddThemeFontSizeOverride("font_size", 14);
            v.AddChild(_syncDom);

            _status = new Label { Text = "" };
            UiTheme.StyleLabel(_status, 14, UiTheme.Text);
            v.AddChild(_status);

            var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
            actions.AddThemeConstantOverride("separation", 12);
            v.AddChild(actions);
            var apply = UiTheme.MakeButton(UiText.T("应用", "Apply"), 15);
            apply.CustomMinimumSize = new Vector2(120f, 40f);
            apply.Pressed += Apply;
            actions.AddChild(apply);
            var cancel = UiTheme.MakeButton(UiText.T("关闭", "Close"), 15);
            cancel.CustomMinimumSize = new Vector2(100f, 40f);
            cancel.Pressed += HidePanel;
            actions.AddChild(cancel);
        }

        private static IRunState? RunState() => RunManager.Instance?.DebugOnlyGetState();
        private static bool SecondBossExists() { try { return RunState()?.Act?.HasSecondBoss ?? false; } catch { return false; } }
        private bool SecondSelected() => _which.Selected == 1;

        private void TryShow()
        {
            var st = RunState();
            if (st?.Act == null) { SyncToast.Show(UiText.T("当前不在跑局中。", "Not in a run.")); return; }

            // (Re)build which-boss options with CURRENT (localized) names.
            _which.Clear();
            string mainName = SafeName(st, false), secondName = SafeName(st, true);
            _which.AddItem(UiText.T($"主 Boss：{mainName}", $"Main boss: {mainName}"));
            if (SecondBossExists())
                _which.AddItem(UiText.T($"第二 Boss：{secondName}", $"Second boss: {secondName}"));
            _which.Selected = 0;

            _syncDom.ButtonPressed = true;
            bool canEdit = NetAuth.CanEdit();
            if (!canEdit)
                _status.Text = UiText.T("客机只读：只有主机可以修改 Boss。", "Read-only for clients: only the host can change the boss.");
            else
                _status.Text = "";

            _root.Visible = true;
            RebuildList();
        }

        private static string SafeName(IRunState st, bool second)
        {
            try
            {
                var enc = second ? st.Act.SecondBossEncounter : st.Act.BossEncounter;
                return enc?.Title?.GetRawText() ?? (enc?.Id?.Entry ?? "?");
            }
            catch { return "?"; }
        }

        private void RebuildList()
        {
            try
            {
                foreach (var c in _listBox.GetChildren()) c.QueueFree();
                _optionIds.Clear();
                bool second = SecondSelected();
                var bosses = BossCatalog.List(second);
                _header.Text = UiText.T(
                    second ? $"当前编辑：第二 Boss（{SafeName(RunState()!, true)}）" : $"当前编辑：主 Boss（{SafeName(RunState()!, false)}）",
                    second ? $"Editing SECOND boss ({SafeName(RunState()!, true)})" : $"Editing MAIN boss ({SafeName(RunState()!, false)})");

                _selectedId = "";
                foreach (var b in bosses)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 10);
                    var thumb = new TextureRect
                    {
                        CustomMinimumSize = new Vector2(96f, 96f),
                        SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                        SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                        Texture = TryLoadThumb(b.Id),
                    };
                    row.AddChild(thumb);
                    string label = (b.IsCurrent ? "★ " : "") + b.Title;
                    var btn = UiTheme.MakeButton(label, 20);
                    btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    btn.CustomMinimumSize = new Vector2(0f, 96f);
                    btn.Alignment = HorizontalAlignment.Left;
                    btn.ToggleMode = true;
                    string id = b.Id;
                    btn.Pressed += () => { _selectedId = id; HighlightSelection(); UpdateStatus(); };
                    row.AddChild(btn);
                    if (b.IsCurrent) { _selectedId = b.Id; btn.ButtonPressed = true; }
                    _listBox.AddChild(row);
                    _optionIds.Add(id);
                }
                UpdateStatus();
            }
            catch (Exception e) { Log.Warn("boss list: " + e.Message); }
        }

        private void HighlightSelection()
        {
            // Visual selection is handled by ToggleMode buttons; ensure only one appears pressed.
            try
            {
                int idx = 0;
                foreach (var child in _listBox.GetChildren())
                {
                    if (child is HBoxContainer h && h.GetChildCount() > 1 && h.GetChild(1) is Button btn)
                    {
                        btn.ButtonPressed = idx < _optionIds.Count && _optionIds[idx] == _selectedId;
                    }
                    idx++;
                }
            }
            catch { }
        }

        private void UpdateStatus()
        {
            try
            {
                if (!NetAuth.CanEdit()) return;
                string extra = "";
                if (CombatExchange.Active)
                {
                    double cost = Settings.Current.ExchangeCostBoss * CombatExchange.Multiplier();
                    extra = UiText.T($"（消耗 {cost:0} EMC，当前 {CombatExchange.DisplayPoints:0}）", $" (cost {cost:0} EMC, have {CombatExchange.DisplayPoints:0})");
                }
                _status.Text = string.IsNullOrEmpty(_selectedId)
                    ? UiText.T("请选择一个 Boss。", "Pick a boss.")
                    : UiText.T("已选择，点击应用。", "Selected; click Apply.") + extra;
            }
            catch { }
        }

        /// <summary>Load a boss thumbnail from several candidate locations (placeholder art, run-history
        /// icon, boss-node art). Returns null if none exist.</summary>
        private Texture2D? TryLoadThumb(string bossId)
        {
            try
            {
                if (string.IsNullOrEmpty(bossId)) return null;
                string lower = bossId.ToLowerInvariant();
                var candidates = new List<string>
                {
                    $"res://images/map/placeholder/{lower}_icon.png",
                    $"res://images/map/placeholder/{lower}.png",
                };
                try
                {
                    var enc = BossCatalog.Resolve(bossId);
                    if (enc != null)
                    {
                        try
                        {
                            var p = MegaCrit.Sts2.Core.Helpers.ImageHelper.GetRoomIconPath(
                                MegaCrit.Sts2.Core.Map.MapPointType.Boss, MegaCrit.Sts2.Core.Rooms.RoomType.Boss, enc.Id);
                            if (!string.IsNullOrEmpty(p)) candidates.Add(p!);
                        }
                        catch { }
                        try { candidates.Add(enc.BossNodePath + ".png"); } catch { }
                    }
                }
                catch { }

                foreach (var c in candidates)
                {
                    try { if (!string.IsNullOrEmpty(c) && ResourceLoader.Exists(c)) return ResourceLoader.Load<Texture2D>(c, null, ResourceLoader.CacheMode.Reuse); }
                    catch { }
                }
            }
            catch (Exception e) { Log.Warn("boss thumb: " + e.Message); }
            return null;
        }

        private void Apply()
        {
            try
            {
                if (!NetAuth.CanEdit()) { _status.Text = UiText.T("只有主机可以修改 Boss。", "Only the host can change the boss."); return; }
                if (string.IsNullOrEmpty(_selectedId)) { _status.Text = UiText.T("请选择一个 Boss。", "Pick a boss."); return; }

                if (CombatExchange.Active)
                {
                    double cost = Settings.Current.ExchangeCostBoss * CombatExchange.Multiplier();
                    if (CombatExchange.Points + 1e-9 < cost) { _status.Text = UiText.T("EMC 不足。", "Not enough EMC."); return; }
                    if (!CombatExchange.TrySpend(cost)) { _status.Text = UiText.T("EMC 不足。", "Not enough EMC."); return; }
                }

                bool second = SecondSelected();
                if (BossService.ApplyAndSync(second, _selectedId, _syncDom.ButtonPressed, out var err))
                    _status.Text = UiText.T("已应用。", "Applied.");
                else
                    _status.Text = UiText.T("应用失败：" + err, "Failed: " + err);
            }
            catch (Exception e) { Log.Warn("boss apply: " + e.Message); }
        }

        private void HidePanel()
        {
            if (!_built) return;
            _root.Visible = false;
            GetViewport()?.GuiReleaseFocus();
        }

        public void OnHoverChanged() { }
    }
}
