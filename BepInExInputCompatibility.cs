using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ConfigurationManager
{
    internal static class BepInExInputCompatibility
    {
        private static readonly KeyCode[] AdditionalShortcutKeys =
        {
            KeyCode.Mouse1,
            KeyCode.Mouse2,
            KeyCode.Mouse3,
            KeyCode.Mouse4
        };

        internal static KeyCode[] GetSupportedShortcutKeys(IInputSystem input)
        {
            return input.SupportedKeyCodes
                .Concat(AdditionalShortcutKeys)
                .Where(key => key != KeyCode.Mouse0 && key != KeyCode.None)
                .Distinct()
                .ToArray();
        }

        private static ButtonControl GetMouseControl(KeyCode key)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return null;

            switch (key)
            {
                case KeyCode.Mouse0:
                    return mouse.leftButton;
                case KeyCode.Mouse1:
                    return mouse.rightButton;
                case KeyCode.Mouse2:
                    return mouse.middleButton;
                case KeyCode.Mouse3:
                    return mouse.backButton;
                case KeyCode.Mouse4:
                    return mouse.forwardButton;
                default:
                    return null;
            }
        }

        [HarmonyPatch]
        private static class NewInputSystemMouseKeyPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                Type type = AccessTools.TypeByName("BepInEx.NewInputSystem");
                if (type == null)
                    yield break;

                foreach (string methodName in new[] { "GetKey", "GetKeyDown", "GetKeyUp" })
                {
                    MethodInfo method = AccessTools.Method(type, methodName, new[] { typeof(KeyCode) });
                    if (method != null)
                        yield return method;
                }
            }

            [HarmonyPrefix]
            private static bool Prefix(MethodBase __originalMethod, KeyCode __0, ref bool __result)
            {
                ButtonControl control = GetMouseControl(__0);
                if (control == null)
                    return true;

                switch (__originalMethod.Name)
                {
                    case "GetKey":
                        __result = control.isPressed;
                        break;
                    case "GetKeyDown":
                        __result = control.wasPressedThisFrame;
                        break;
                    case "GetKeyUp":
                        __result = control.wasReleasedThisFrame;
                        break;
                    default:
                        return true;
                }

                return false;
            }
        }
    }
}
