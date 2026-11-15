using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace NodeForge.Patches
{
    /// <summary>Attach overlay layers when the game root is ready.</summary>
    [HarmonyPatch(typeof(NGame), "_Ready")]
    internal static class NGameReadyPatch
    {
        private static void Postfix(NGame __instance)
        {
            try
            {
                Menu1Overlay.EnsureAttached(__instance);
                Menu2Overlay.EnsureAttached(__instance);
                BossOverlay.EnsureAttached(__instance);
            }
            catch (System.Exception e) { Log.Warn("attach overlays: " + e.Message); }
        }
    }

    /// <summary>Re-ensure overlays when returning to the main menu (scene rebuilt).</summary>
    [HarmonyPatch(typeof(NMainMenu), "_Ready")]
    internal static class NMainMenuReadyPatch
    {
        private static void Postfix()
        {
            try
            {
                var game = NGame.Instance;
                if (game != null)
                {
                    Menu1Overlay.EnsureAttached(game);
                    Menu2Overlay.EnsureAttached(game);
                    BossOverlay.EnsureAttached(game);
                }
            }
            catch (System.Exception e) { Log.Warn("attach overlays (menu): " + e.Message); }
        }
    }
}

