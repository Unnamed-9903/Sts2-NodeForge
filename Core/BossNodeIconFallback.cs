using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace NodeForge
{
    /// <summary>
    /// Fallback for Boss map-node art: some bosses (e.g. The Insatiable, Queen) use Spine art that may not
    /// be preloaded after a boss swap, leaving the node blank. After a rebuild we force the node to show a
    /// static icon (the same run-history boss image used by the top bar), which is always available.
    /// </summary>
    internal static class BossNodeIconFallback
    {
        public static void ApplyDeferred()
        {
            try
            {
                var screen = NMapScreen.Instance;
                if (screen == null) return;
                var timer = screen.GetTree()?.CreateTimer(0.05, true, false, false);
                if (timer != null) timer.Timeout += DoApply;
                else DoApply();
            }
            catch (Exception e) { Log.Warn("boss icon fallback schedule: " + e.Message); }
        }

        private static void DoApply()
        {
            try
            {
                var screen = NMapScreen.Instance;
                if (screen == null) return;
                var st = MegaCrit.Sts2.Core.Runs.RunManager.Instance?.DebugOnlyGetState();
                if (st?.Act == null) return;

                ForceStatic(screen, false, st.Act.BossEncounter);
                try
                {
                    if (st.Act.HasSecondBoss && st.Act.SecondBossEncounter != null)
                        ForceStatic(screen, true, st.Act.SecondBossEncounter);
                }
                catch { }
            }
            catch (Exception e) { Log.Warn("boss icon fallback: " + e.Message); }
        }

        private static void ForceStatic(NMapScreen screen, bool second, EncounterModel? enc)
        {
            try
            {
                if (enc == null) return;
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                string field = second ? "_secondBossPointNode" : "_bossPointNode";
                var node = typeof(NMapScreen).GetField(field, F)?.GetValue(screen) as Node;
                if (node == null) return;

                // Hide only the Spine sprite (NOT its container: PlaceholderImage may live inside it).
                try
                {
                    var sp = node.GetType().GetField("_spineSprite", F)?.GetValue(node) as Node2D
                             ?? node.GetNodeOrNull<Node2D>("%SpineSprite");
                    if (sp != null) sp.Visible = false;
                }
                catch { }

                string id = "";
                try { id = enc.Id.Entry ?? ""; } catch { }
                string lower = id.ToLowerInvariant();

                // Try several known static-icon locations; use the first that exists.
                var candidates = new System.Collections.Generic.List<(string icon, string outline)>
                {
                    ($"res://images/map/placeholder/{lower}_icon.png", $"res://images/map/placeholder/{lower}_icon_outline.png"),
                    ($"res://images/map/placeholder/{lower}.png",       $"res://images/map/placeholder/{lower}_outline.png"),
                };
                try
                {
                    var p1 = MegaCrit.Sts2.Core.Helpers.ImageHelper.GetRoomIconPath(
                        MegaCrit.Sts2.Core.Map.MapPointType.Boss, MegaCrit.Sts2.Core.Rooms.RoomType.Boss, enc.Id);
                    var p2 = MegaCrit.Sts2.Core.Helpers.ImageHelper.GetRoomIconOutlinePath(
                        MegaCrit.Sts2.Core.Map.MapPointType.Boss, MegaCrit.Sts2.Core.Rooms.RoomType.Boss, enc.Id);
                    if (!string.IsNullOrEmpty(p1)) candidates.Add((p1!, p2 ?? ""));
                }
                catch { }
                try
                {
                    var bp = enc.BossNodePath;
                    if (!string.IsNullOrEmpty(bp)) candidates.Add((bp + ".png", bp + "_outline.png"));
                }
                catch { }

                string iconPath = "", outlinePath = "";
                foreach (var (ic, ol) in candidates)
                {
                    bool ok = false;
                    try { ok = !string.IsNullOrEmpty(ic) && ResourceLoader.Exists(ic); } catch { }
                    if (ok) { iconPath = ic; outlinePath = ol; break; }
                }
                Log.Info($"boss icon fallback: {(second ? "second" : "main")} {id} -> '{(string.IsNullOrEmpty(iconPath) ? "<none found>" : iconPath)}'");
                if (string.IsNullOrEmpty(iconPath)) return;

                var img = node.GetType().GetField("_placeholderImage", F)?.GetValue(node) as TextureRect
                          ?? node.GetNodeOrNull<TextureRect>("%PlaceholderImage");
                var outline = node.GetType().GetField("_placeholderOutline", F)?.GetValue(node) as TextureRect
                              ?? node.GetNodeOrNull<TextureRect>("%PlaceholderOutline");

                if (img != null)
                {
                    img.Texture = ResourceLoader.Load<Texture2D>(iconPath, null, ResourceLoader.CacheMode.Reuse);
                    img.Visible = true;
                }
                if (outline != null && !string.IsNullOrEmpty(outlinePath) && ResourceLoader.Exists(outlinePath))
                {
                    outline.Texture = ResourceLoader.Load<Texture2D>(outlinePath, null, ResourceLoader.CacheMode.Reuse);
                    outline.Visible = true;
                }

                // Guaranteed-visible overlay: add our own TextureRect on the boss node (independent of the
                // internal placeholder/spine structure), so the icon shows regardless of scene layout.
                try
                {
                    var existing = node.GetNodeOrNull<TextureRect>("NodeForgeBossIcon");
                    if (existing == null)
                    {
                        existing = new TextureRect
                        {
                            Name = "NodeForgeBossIcon",
                            MouseFilter = Control.MouseFilterEnum.Ignore,
                            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                            ZIndex = 5,
                        };
                        existing.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                        node.AddChild(existing);
                    }
                    existing.Texture = ResourceLoader.Load<Texture2D>(iconPath, null, ResourceLoader.CacheMode.Reuse);
                    existing.Visible = true;
                }
                catch (Exception e) { Log.Warn("boss icon overlay: " + e.Message); }

                Log.Info($"boss icon fallback applied: {(second ? "second" : "main")} {enc.Id.Entry}");
            }
            catch (Exception e) { Log.Warn("boss icon force static: " + e.Message); }
        }
    }
}
