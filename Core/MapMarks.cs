using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using NodeForge.UI;

namespace NodeForge
{
    /// <summary>
    /// Marks edited nodes with a lightweight falling-particle effect (CPU particles, one-shot).
    /// Purely visual; honours the MarkerEffectEnabled / colour settings. Cleared on new run/map.
    /// </summary>
    internal static class MapMarks
    {
        private static readonly HashSet<MapCoord> _edited = new();

        public static void MarkEdited(MapCoord coord) { _edited.Add(coord); Draw(coord); }
        public static bool IsMarked(MapCoord coord) => _edited.Contains(coord);
        public static IReadOnlyCollection<MapCoord> Edited => _edited;

        /// <summary>Remove the mark + particle effect for a single coord (used when a node is reverted).</summary>
        public static void Unmark(MapCoord coord)
        {
            try
            {
                _edited.Remove(coord);
                var screen = NMapScreen.Instance;
                if (screen == null) return;
                var node = GetNode(screen, coord);
                if (node is Node n) RemoveEffect(n);
            }
            catch (Exception e) { Log.Warn("unmark fx: " + e.Message); }
        }

        /// <summary>Clear all marks (called on new map/run so the effect never persists across runs).</summary>
        public static void Clear()
        {
            _edited.Clear();
            try
            {
                var screen = NMapScreen.Instance;
                if (screen == null) return;
                var f = typeof(NMapScreen).GetField("_mapPointDictionary",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(screen) is System.Collections.IDictionary dict)
                {
                    foreach (System.Collections.DictionaryEntry e in dict)
                        if (e.Value is Node n) RemoveEffect(n);
                }
            }
            catch { }
        }

        public static void RedrawAll()
        {
            if (!Settings.Current.MarkerEffectEnabled) { ClearEffectsOnly(); return; }
            foreach (var c in new List<MapCoord>(_edited)) Draw(c);
        }

        private static void Draw(MapCoord coord)
        {
            try
            {
                if (!Settings.Current.MarkerEffectEnabled) return;
                var screen = NMapScreen.Instance;
                if (screen == null) return;
                var node = GetNode(screen, coord);
                if (node is not Control nodeCtl) return;

                RemoveEffect(nodeCtl);

                var col = UiTheme.ParseColor(Settings.Current.MarkRingColor, new Color("FF3838"));

                // Continuous emitter that keeps spawning particles around the node while the map is open.
                var p = new CpuParticles2D
                {
                    Name = "NodeForgeMarkFx",
                    Amount = 16,
                    Lifetime = 1.6,
                    OneShot = false,
                    Emitting = true,
                    LocalCoords = true,                      // follow the node (not the screen)
                    Direction = new Vector2(0, 1),
                    Spread = 180f,
                    Gravity = new Vector2(0, 20f),
                    InitialVelocityMin = 4f,
                    InitialVelocityMax = 18f,
                    ScaleAmountMin = 1f,
                    ScaleAmountMax = 2f,
                    ZIndex = 0,
                    ShowBehindParent = false,
                };
                // White texture; colour applied once via Color + a white->colour ramp so it never double-tints.
                var img = Image.CreateEmpty(6, 6, false, Image.Format.Rgba8);
                img.Fill(Colors.White);
                p.Texture = ImageTexture.CreateFromImage(img);

                var ramp = new Gradient();
                ramp.SetColor(0, new Color(col.R, col.G, col.B, 1f));
                ramp.SetColor(1, new Color(col.R, col.G, col.B, 0f));
                p.Color = col;
                p.ColorRamp = ramp;

                Vector2 nodeSize = nodeCtl.Size;
                if (nodeSize.X <= 1f || nodeSize.Y <= 1f) nodeSize = new Vector2(48f, 48f);
                p.Position = nodeSize / 2f;
                p.EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere;
                p.EmissionSphereRadius = Mathf.Max(nodeSize.X, nodeSize.Y) * 0.6f;

                nodeCtl.CallDeferred(Node.MethodName.AddChild, p);
            }
            catch (Exception e) { Log.Warn("draw fx: " + e.Message); }
        }

        private static void ClearEffectsOnly()
        {
            try
            {
                var screen = NMapScreen.Instance;
                if (screen == null) return;
                var f = typeof(NMapScreen).GetField("_mapPointDictionary",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(screen) is System.Collections.IDictionary dict)
                    foreach (System.Collections.DictionaryEntry e in dict)
                        if (e.Value is Node n) RemoveEffect(n);
            }
            catch { }
        }

        private static void RemoveEffect(Node n)
        {
            foreach (var child in n.GetChildren())
                if (child is Node c && c.Name.ToString() == "NodeForgeMarkFx") c.QueueFree();
        }

        private static Node? GetNode(NMapScreen screen, MapCoord coord)
        {
            try
            {
                var f = typeof(NMapScreen).GetField("_mapPointDictionary",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f?.GetValue(screen) is System.Collections.IDictionary dict && dict.Contains(coord))
                    return dict[coord] as Node;
            }
            catch { }
            return null;
        }
    }
}

