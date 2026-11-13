using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;

namespace NodeForge
{
    [ModInitializer("Initialize")]
    internal static class ModEntry
    {
        public const string ModId = "NodeForge";
        public const string ModDisplayName = "NodeForge";

        public static void Initialize()
        {
            try
            {
                // 1) Local file fallback settings first (so gameplay works even if RitsuLib store is missing).
                Settings.Load();

                // 2) Attach RitsuLib data store and register the settings page.
                try
                {
                    var store = RitsuLibFramework.GetDataStore(ModId);
                    Settings.AttachStore(store);
                }
                catch (Exception e) { Log.Warn("GetDataStore failed: " + e.Message); }

                SettingsRegistration.Register();

                // 3) Harmony patches.
                new Harmony(ModId).PatchAll(Assembly.GetExecutingAssembly());

                // 4) Warm up the event catalogue (offline scan + incremental cache).
                try { EventCatalog.EnsureLoaded(); } catch (Exception e) { Log.Warn("event catalog: " + e.Message); }

                // 5) Register the Sidecar handler so clients receive host commands.
                try { NodeForgeNet.EnsureRegistered(); } catch (Exception e) { Log.Warn("net register: " + e.Message); }
                try { STS2RitsuLib.Networking.Sidecar.RitsuLibSidecarProtocol.EnsureDefaultHandlers(); } catch (Exception e) { Log.Warn("sidecar init: " + e.Message); }

                Log.Info("Initialized. " + Settings.Current.Describe());
            }
            catch (Exception e)
            {
                Log.Error("Initialize failed: " + e);
            }
        }
    }
}
