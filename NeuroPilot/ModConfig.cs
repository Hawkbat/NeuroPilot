using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroPilot
{
    public static class ModConfig
    {
        public static string NeuroApiUrl => Get<string>("Neuro API URL");

        public static bool DebugMode => Get<bool>("Debug Mode");
        public static bool ManualOverride => Get<bool>("Manual Override");
        public static bool AllowDestructive => !Get<bool>("Prevent Destructive Actions");

        public static bool ScoutLauncher_Neuro => Get<bool>("Scout Launcher (Neuro)");
        public static bool ScoutLauncher_Manual => Get<bool>("Scout Launcher (Manual Control)");

        public static T Get<T>(string key) =>
            NeuroPilot.instance.ModHelper.Config.GetSettingsValue<T>(key);
    }
}
