using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using NodeForge.UI;

namespace NodeForge
{
    /// <summary>
    /// Menu 1 overlay: bulk replace selected source node types into a target type.
    /// Functionally the original nodeswap panel, re-skinned (teal/amber) and wired to the new settings model.
    /// </summary>
    internal sealed class Menu1Overlay : CanvasLayer
    {
        private static Menu1Overlay? _instance;

        private bool _built;
        private ColorRect _backdrop = null!;
        private PanelContainer _panel = null!;
        private CenterContainer _center = null!;
        private OptionButton _target = null!;
        private readonly Dictionary<string, CheckButton> _sources = new();
        private Label _status = null!;

        private static string T(string zh, string en) => UiText.T(zh, en);

        public static void Toggle(NGame game)
        {
            EnsureAttached(game);
            if (_instance == null) return;
            if (_instance._center.Visible) _instance.HidePanel(); else _instance.ShowPanel();
        }

        public static bool CloseIfOpen()
        {
            if (_instance?._center is { Visible: true }) { _instance.HidePanel(); return true; }
            return false;
        }

        public static void EnsureAttached(NGame game)
        {
            if (_instance != null && GodotObject.IsInstanceValid(_instance))
            {
                if (_instance.GetParent() == null) game.AddChild(_instance);
                _instance.Init();
                return;
            }
            _instance = new Menu1Overlay { Name = "NodeForgeMenu1" };
            _instance.Init();
            game.AddChild(_instance);
        }

        private void Init()
        {
            Layer = 101;
            ProcessMode = ProcessModeEnum.Always;
            if (!_built) { _built = true; _builtLang = CurLang(); Build(); }
            SyncState();
        }

        private void Build()
        {
            _backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.72f), Visible = false };
            _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _backdrop.GuiInput += OnBackdropInput;
            AddChild(_backdrop);

            _center = new CenterContainer { Visible = false };
            _center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _center.MouseFilter = Control.MouseFilterEnum.Ignore;
            AddChild(_center);

            _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
            _center.AddChild(_panel);
            _panel.CustomMinimumSize = new Vector2(600f, 620f);
            _panel.AddThemeStyleboxOverride("panel", UiTheme.Panel());

            var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _panel.AddChild(root);

            var title = new Label { Text = T("节点批量替换", "Bulk Node Replace"), HorizontalAlignment = HorizontalAlignment.Center };
            UiTheme.StyleLabel(title, 26, UiTheme.Accent);
            root.AddChild(title);

            var sub = new Label
            {
                Text = T("勾选要替换的来源节点，再选择目标节点，点“应用到当前地图”立即生效。",
                         "Pick source node types and a target, then click 'Apply to current map'."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            UiTheme.StyleLabel(sub, 14, UiTheme.TextDim);
            root.AddChild(sub);

            root.AddChild(new HSeparator());

            // Target selector
            var targetRow = new HBoxContainer();
            var tLabel = new Label { Text = T("替换为：", "Replace with:"), CustomMinimumSize = new Vector2(200f, 0f) };
            UiTheme.StyleLabel(tLabel, 18, UiTheme.Text);
            targetRow.AddChild(tLabel);
            _target = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _target.AddThemeFontSizeOverride("font_size", 16);
            foreach (var o in NodeTypeCatalog.Menu1Targets) _target.AddItem(o.Label);
            targetRow.AddChild(_target);
            root.AddChild(targetRow);

            root.AddChild(new HSeparator());

            var srcLabel = new Label { Text = T("来源节点", "Source nodes") };
            UiTheme.StyleLabel(srcLabel, 18, UiTheme.Accent);
            root.AddChild(srcLabel);

            AddSource(root, "question", T("问号 (?)", "Question (?)"), o => o.ConvertQuestion, (o, v) => o.ConvertQuestion = v);
            AddSource(root, "elite", T("精英", "Elite"), o => o.ConvertElite, (o, v) => o.ConvertElite = v);
            AddSource(root, "monster", T("普通战斗", "Normal combat"), o => o.ConvertMonster, (o, v) => o.ConvertMonster = v);
            AddSource(root, "shop", T("商店", "Shop"), o => o.ConvertShop, (o, v) => o.ConvertShop = v);
            AddSource(root, "rest_site", T("火堆", "Campfire"), o => o.ConvertRestSite, (o, v) => o.ConvertRestSite = v);
            AddSource(root, "treasure", T("宝箱", "Treasure"), o => o.ConvertTreasure, (o, v) => o.ConvertTreasure = v);

            _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            UiTheme.StyleLabel(_status, 14, UiTheme.Ok);
            root.AddChild(_status);

            var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            root.AddChild(spacer);

            var actions1 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            var all = UiTheme.MakeButton(T("全选", "All"));
            all.Pressed += () => { foreach (var cb in _sources.Values) cb.ButtonPressed = true; };
            var none = UiTheme.MakeButton(T("全不选", "None"));
            none.Pressed += () => { foreach (var cb in _sources.Values) cb.ButtonPressed = false; };
            actions1.AddChild(all); actions1.AddChild(none);
            root.AddChild(actions1);

            // Session-only option (default OFF): auto-apply this rule to newly generated maps.
            // Singleplayer only (multiplayer would diverge host/client maps). Not shown in settings page.
            _autoFuture = new CheckBox
            {
                Text = T("后续地图自动替换（仅本次游戏，默认关）", "Auto-replace future maps (this session, off by default)"),
            };
            _autoFuture.AddThemeFontSizeOverride("font_size", 13);
            _autoFuture.Toggled += on => { try { Settings.Current.AutoApplyFutureMaps = on; } catch { } };
            root.AddChild(_autoFuture);

            var actions2 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            var applyNow = UiTheme.MakeButton(T("应用到当前地图", "Apply to current map"), 15);
            applyNow.CustomMinimumSize = new Vector2(170f, 40f);
            applyNow.Pressed += ApplyToCurrentMap;
            _applyBtn = applyNow;
            var undoBulk = UiTheme.MakeButton(T("撤销一次批量", "Undo one bulk"), 15);
            undoBulk.CustomMinimumSize = new Vector2(150f, 40f);
            undoBulk.Pressed += UndoBulk;
            _undoBulkBtn = undoBulk;
            var saveCfg = UiTheme.MakeButton(T("仅保存配置", "Save config"), 14);
            saveCfg.Pressed += ApplyFromUi;
            var close = UiTheme.MakeButton(T("关闭", "Close"), 14);
            close.Pressed += HidePanel;
            actions2.AddChild(applyNow); actions2.AddChild(undoBulk); actions2.AddChild(saveCfg); actions2.AddChild(close);
            root.AddChild(actions2);
        }

        private Button? _undoBulkBtn;
        private Button? _applyBtn;
        private CheckBox? _autoFuture;

        private static MegaCrit.Sts2.Core.Runs.IRunState? RunState()
        {
            try { return MegaCrit.Sts2.Core.Runs.RunManager.Instance?.DebugOnlyGetState(); } catch { return null; }
        }
        private static MegaCrit.Sts2.Core.Map.ActMap? CurrentMap()
        {
            try { return RunState()?.Map; } catch { return null; }
        }

        private void AddSource(VBoxContainer root, string id, string text,
            Func<NodeForgeSettings, bool> get, Action<NodeForgeSettings, bool> set)
        {
            var cb = new CheckButton { Text = text };
            cb.AddThemeFontSizeOverride("font_size", 18);
            cb.Toggled += v => { var c = Settings.Current; set(c, v); };
            _sources[id] = cb;
            root.AddChild(cb);
        }

        private void ApplyFromUi()
        {
            var cfg = Settings.Current;
            int idx = _target.Selected;
            if (idx >= 0 && idx < NodeTypeCatalog.All.Count)
                cfg.TargetNode = NodeTypeCatalog.All[idx].ConfigId;
            // Conversion switches/target are session-only (not persisted).
            _status.Text = T($"本次会话设置：目标 = {NodeTypeCatalog.ToLabel(cfg.TargetPointType)}。点“应用到当前地图”立即生效。",
                             $"Session target = {NodeTypeCatalog.ToLabel(cfg.TargetPointType)}. Click 'Apply to current map'.");
        }

        private void ApplyToCurrentMap()
        {
            if (!NetAuth.CanEdit())
            {
                _toast(T("只有主机可以修改地图节点。", "Only the host may edit map nodes."));
                return;
            }
            var cfg = Settings.Current;
            int idx = _target.Selected;
            string targetCfgId = (idx >= 0 && idx < NodeTypeCatalog.Menu1Targets.Count)
                ? NodeTypeCatalog.Menu1Targets[idx].ConfigId : cfg.TargetNode;
            cfg.TargetNode = targetCfgId;
            // Do NOT persist conversion settings.

            var map = CurrentMap();
            if (map == null)
            {
                _status.Text = T("当前不在跑局中，无法对当前地图执行。", "Not in a run; cannot apply to current map.");
                return;
            }
            if (!NetAuth.BatchAllowed())
            {
                _status.AddThemeColorOverride("font_color", UiTheme.Warn);
                _status.Text = T("批量替换需在跑局中进行（进入游戏后）。",
                                 "Bulk replace is only available during a run.");
                return;
            }
            // Unified bulk path (same as Menu 2): protection + marker + optional forced event.
            var snap = MapNodeService.BulkApply(map, RunState(),
                t => MapNodeService.MatchesMenu1Rule(t, cfg), targetCfgId, "", "Menu1", broadcast: true);
            BulkHistory.Push(snap);
            try { MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance?.RefreshAllPointVisuals(); } catch { }
            TrySave();
            UpdateUndoBulk();
            _status.Text = T($"已对当前地图替换 {snap.Changed.Count} 个节点 → {NodeTypeCatalog.Menu1TargetLabel(targetCfgId)}。",
                             $"Replaced {snap.Changed.Count} nodes on current map -> {NodeTypeCatalog.Menu1TargetLabel(targetCfgId)}.");
        }

        private void UndoBulk()
        {
            if (!BulkHistory.CanUndo) { UpdateUndoBulk(); return; }
            var snap = BulkHistory.Pop();
            if (snap == null) { UpdateUndoBulk(); return; }
            var map = CurrentMap();
            if (map == null) { _status.Text = T("当前不在跑局中。", "Not in a run."); UpdateUndoBulk(); return; }
            // Undo through the same command path (locally applied + broadcast if host) with protection.
            var prot = MapNodeService.CollectProtected(map, RunState());
            NodeForgeNet.BeginBatch();
            try
            {
                foreach (var (coord, prev) in snap.Changed)
                {
                    if (prot.Contains(coord)) continue;
                    NodeForgeNet.SubmitRevert(coord, prev, false, null, 0);
                }
            }
            finally { NodeForgeNet.EndBatch(); }
            try { MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance?.RefreshAllPointVisuals(); } catch { }
            TrySave();
            UpdateUndoBulk();
            _status.Text = T($"已撤销一次批量（还原 {snap.Changed.Count} 个节点）。", $"Undid one bulk ({snap.Changed.Count} nodes).");
        }

        private void UpdateUndoBulk()
        {
            if (_undoBulkBtn != null) _undoBulkBtn.Disabled = !BulkHistory.CanUndo;
        }

        private static void TrySave()
        {
            try
            {
                var sm = MegaCrit.Sts2.Core.Saves.SaveManager.Instance;
                if (sm != null) MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(sm.SaveRun(null));
            }
            catch { }
        }

        private Label? _toastLabel;
        private SceneTreeTimer? _toastTimer;

        private void _toast(string msg)
        {
            try
            {
                if (_toastLabel == null || !GodotObject.IsInstanceValid(_toastLabel))
                {
                    _toastLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
                    _toastLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
                    _toastLabel.Position = new Vector2(-300f, 120f);
                    _toastLabel.CustomMinimumSize = new Vector2(600f, 0f);
                    _toastLabel.AddThemeFontSizeOverride("font_size", 20);
                    _toastLabel.AddThemeColorOverride("font_color", new Color("FF5555"));
                    AddChild(_toastLabel);
                }
                _toastLabel.Text = msg;
                _toastLabel.Visible = true;
                try { _toastTimer?.Disconnect(SceneTreeTimer.SignalName.Timeout, Callable.From(OnToastTimeout)); } catch { }
                _toastTimer = GetTree().CreateTimer(5.0, true, false, false);
                _toastTimer.Timeout += OnToastTimeout;
            }
            catch { }
        }

        private void OnToastTimeout()
        {
            try { if (_toastLabel != null && GodotObject.IsInstanceValid(_toastLabel)) _toastLabel.Visible = false; } catch { }
        }

        private void RefreshFromConfig()
        {
            var cfg = Settings.Current;
            int sel = 0;
            for (int i = 0; i < NodeTypeCatalog.Menu1Targets.Count; i++)
                if (string.Equals(NodeTypeCatalog.Menu1Targets[i].ConfigId, cfg.TargetNode, StringComparison.OrdinalIgnoreCase)) { sel = i; break; }
            _target.Selected = sel;

            SetToggle("question", cfg.ConvertQuestion);
            SetToggle("elite", cfg.ConvertElite);
            SetToggle("monster", cfg.ConvertMonster);
            SetToggle("shop", cfg.ConvertShop);
            SetToggle("rest_site", cfg.ConvertRestSite);
            SetToggle("treasure", cfg.ConvertTreasure);
            SetToggle("ancient", cfg.ConvertAncient);

        }

        private void SetToggle(string id, bool v)
        {
            if (_sources.TryGetValue(id, out var cb)) cb.ButtonPressed = v;
        }

        private string _builtLang = "";
        private static string CurLang()
        {
            try { return MegaCrit.Sts2.Core.Localization.LocManager.Instance?.Language ?? "zhs"; }
            catch { return "zhs"; }
        }
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
            catch (Exception e) { Log.Warn("menu1 rebuild (lang): " + e.Message); }
        }

        private void ShowPanel()
        {
            if (!_built) return;
            EnsureLanguageRebuilt();
            if (!_built) return;
            if (!NetAuth.CanEdit())
            {
                _toast(T("只有主机可以修改地图节点（联机客机不可修改）。", "Only the host may edit map nodes (clients cannot)."));
                return;
            }
            // Mutual exclusion: close Menu 2 if open.
            try { Menu2Overlay.CloseIfOpen(); } catch { }
            RefreshFromConfig();
            UpdateUndoBulk();
            if (_applyBtn != null)
            {
                _applyBtn.Disabled = !NetAuth.BatchAllowed();
                _applyBtn.TooltipText = NetAuth.BatchAllowed() ? "" : T("需进入游戏（跑局中）后才能批量修改", "Bulk editing is only available during a run");
            }
            if (_autoFuture != null)
            {
                bool mp = NetAuth.IsMultiplayer();
                _autoFuture.ButtonPressed = Settings.Current.AutoApplyFutureMaps && !mp;
                _autoFuture.Disabled = mp;
                _autoFuture.TooltipText = mp ? T("联机模式禁用（避免主客地图不一致）", "Disabled in multiplayer (would desync maps)") : "";
            }
            _backdrop.Visible = true;
            _center.Visible = true;
            try
            {
                var vp = GetViewport().GetVisibleRect().Size;
                float w = Mathf.Min(600f, vp.X * 0.92f);
                float h = Mathf.Min(620f, vp.Y * 0.92f);
                _panel.CustomMinimumSize = new Vector2(w, h);
                _panel.Size = new Vector2(w, h);
            }
            catch { }
        }

        private void HidePanel()
        {
            if (!_built) return;
            _backdrop.Visible = false;
            _center.Visible = false;
            GetViewport()?.GuiReleaseFocus();
        }

        private void SyncState()
        {
            if (NGame.Instance == null) HidePanel();
        }

        private void OnBackdropInput(InputEvent e)
        {
            if (e is InputEventMouseButton { Pressed: true }) HidePanel();
        }
    }
}





