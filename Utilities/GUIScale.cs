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

            private struct MatrixState
            {
                public bool Changed;
                public Matrix4x4 Original;
            }

            [HarmonyPriority(Priority.First)]
            private static void Prefix(out MatrixState __state)
            {
                __state = default;

                ConfigurationManager manager = ConfigurationManager.instance;

                if (!manager || !manager.DisplayingWindow ||
                    Mathf.Approximately(manager.ScaleFactor, 1f))
                    return;

                __state.Changed = true;
                __state.Original = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
            }

            [HarmonyPriority(Priority.First)]
            private static void Postfix(MatrixState __state)
            {
                if (__state.Changed)
                    GUI.matrix = __state.Original;
            }
        }
    }
}
