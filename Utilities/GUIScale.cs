using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal class GUIScale
    {
        [HarmonyPatch]
        public static class GUIUtility_GUIPointScaling
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(GUIUtility), nameof(GUIUtility.ScreenToGUIPoint));
                yield return AccessTools.Method(typeof(GUIUtility), nameof(GUIUtility.GUIToScreenPoint));
            }

            [HarmonyPriority(Priority.First)]
            private static void Prefix(ref Matrix4x4 __state)
            {
                if (!ConfigurationManager.instance.DisplayingWindow)
                    return;

                __state = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
            }

            [HarmonyPriority(Priority.First)]
            private static void Postfix(Matrix4x4 __state)
            {
                if (!ConfigurationManager.instance.DisplayingWindow)
                    return;

                GUI.matrix = __state;
            }
        }
    }
}
