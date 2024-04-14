using HarmonyLib;
using static ConfigurationManager.ConfigurationManager;

namespace ValheimConfigurationManager
{
    /// <summary>
    /// Prevent user input when window is open
    /// </summary>
    internal class ValheimPatches
    {
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput))]
        [HarmonyPriority(Priority.Last)]
        public static class Minimap_ShowPinNameInput_PreventPinAddition
        {
            public static void Postfix(ref bool __result)
            {
                if (_preventInput.Value)
                    __result = __result && !instance.DisplayingWindow;
            }
        }

        [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
        [HarmonyPriority(Priority.Last)]
        public static class Minimap_IsOpen_EmulateMinimapOpenStatus
        {
            public static void Postfix(ref bool __result)
            {
                if (_preventInput.Value)
                    __result = __result || instance.DisplayingWindow;
            }
        }
    }
}
