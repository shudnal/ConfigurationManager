using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ConfigurationManager
{
    public partial class ConfigurationManager
    {
        private static int _consoleInputScope;
        private static int _consoleVisibleFrame = -1;
        private static int _inputReleaseFrame = -1;
        private static PreventInput _releasedInputMode;
        private PreventInput _activeInputMode;
        private bool _cursorAcquired;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorRequested;
        private bool _savedCursorVisible;
        private bool _savedHardwareCursorVisible;
        private static readonly List<Vector2> NoTouchPoints = new List<Vector2>();

        private static bool ManagerWindowOpen => instance && instance.isActiveAndEnabled && instance.DisplayingWindow;

        private static PreventInput CurrentInputPrevention
        {
            get
            {
                if (!instance || !instance.isActiveAndEnabled)
                    return PreventInput.Off;
                if (ManagerWindowOpen)
                    return _preventInput?.Value ?? PreventInput.Off;
                return Time.frameCount <= _inputReleaseFrame ? _releasedInputMode : PreventInput.Off;
            }
        }

        private static bool BlockGameInput => CurrentInputPrevention != PreventInput.Off && _consoleInputScope == 0;

        private static bool ConsoleHandlesEscape()
        {
            return CurrentInputPrevention == PreventInput.Player &&
                   (Console.IsVisible() || _consoleVisibleFrame == Time.frameCount);
        }

        private void HandleInputVisibilityChanged(bool visible)
        {
            _inputReleaseFrame = -1;
            if (visible)
            {
                _activeInputMode = _preventInput?.Value ?? PreventInput.Off;
                if (_activeInputMode != PreventInput.Off)
                    CancelInventoryDrag();
            }
            else
            {
                // The closing mouse/key event must not reach a later Update or the next FixedUpdate.
                _releasedInputMode = _activeInputMode;
                _inputReleaseFrame = Time.frameCount + 1;
            }

            if (_activeInputMode != PreventInput.Off)
            {
                ResetGameButtonStates();
                if (!visible)
                    PlayerController.SetTakeInputDelay(Mathf.Max(PlayerController.takeInputDelay, 0.1f));
            }
        }

        private void InputPreventionSettingChanged(object sender, EventArgs args)
        {
            if (!DisplayingWindow)
                return;
            if (_activeInputMode != PreventInput.Off || _preventInput.Value != PreventInput.Off)
                ResetGameButtonStates();
            if (_activeInputMode == PreventInput.Off && _preventInput.Value != PreventInput.Off)
                CancelInventoryDrag();
            _activeInputMode = _preventInput.Value;
        }

        private static void CancelInventoryDrag()
        {
            // Cancel only the presentation of an in-progress drag. The item stays in its source
            // inventory; SetupDragItem(null, ...) neither transfers nor drops it into the world.
            if (InventoryGui.instance)
                InventoryGui.instance.SetupDragItem(null, null, 0);
        }

        private static void ResetGameButtonStates()
        {
            if (ZInput.instance != null)
                ZInput.ResetAllButtonStates();
        }

        private static void ResetInputPrevention()
        {
            _inputReleaseFrame = -1;
            _releasedInputMode = PreventInput.Off;
            _consoleInputScope = 0;
            _consoleVisibleFrame = -1;
        }

        private void AcquireWindowCursor()
        {
            if (!_cursorAcquired)
            {
                _savedCursorLock = ZCursor.LockState;
                _savedCursorRequested = ZCursor.IsRequested;
                _savedCursorVisible = ZCursor.IsVisible;
                _savedHardwareCursorVisible = Cursor.visible;
                _cursorAcquired = true;
            }
            ApplyWindowCursor();
        }

        private static void ApplyWindowCursor()
        {
            if (!ManagerWindowOpen || !Application.isFocused)
                return;

            ZCursor.LockState = CursorLockMode.None;
            if (ZInput.instance != null)
                ZCursor.Show();
            else
                Cursor.visible = true;
        }

        private void ReleaseWindowCursor()
        {
            if (!_cursorAcquired)
                return;
            _cursorAcquired = false;

            if (ZInput.instance != null)
            {
                // Re-evaluate the current UI, not a snapshot from before a scene/menu transition.
                if (GameCamera.instance)
                {
                    GameCamera.instance.UpdateMouseCapture();
                    if (Menu.instance && Menu.IsActive())
                        Menu.instance.UpdateCursor();
                    return;
                }
                if (FejdStartup.instance)
                {
                    FejdStartup.instance.UpdateCursor();
                    return;
                }
                if (Menu.instance && Menu.IsActive())
                {
                    Menu.instance.UpdateCursor();
                    return;
                }
                ZCursor.LockState = _savedCursorLock;
                ZCursor.SetRequested(_savedCursorRequested);
                ZCursor.SetVisible(_savedCursorVisible);
            }
            else
            {
                ZCursor.LockState = _savedCursorLock;
                Cursor.visible = _savedHardwareCursorVisible;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && DisplayingWindow)
                ApplyWindowCursor();
        }

        private static bool AllowUIInput(Component component)
        {
            if (CurrentInputPrevention == PreventInput.Off)
                return true;
            if (!component)
                return false;

            // Only our actual menu button is exempt, not unrelated objects with the same name.
            Button button = component.GetComponent<Button>();
            if (button && instance._menuButtons.Contains(button))
                return true;

            return CurrentInputPrevention == PreventInput.Player && Console.instance &&
                   component.transform.IsChildOf(Console.instance.transform);
        }

        private static MethodInfo InputMethod(Type type, string name)
        {
            return AccessTools.DeclaredMethod(type, name) ?? throw new MissingMethodException(type.FullName, name);
        }

        private static MethodInfo AnalogInputMethod(Type valueType)
        {
            return InputMethod(typeof(ZInput), nameof(ZInput.ReadValueDef)).MakeGenericMethod(valueType);
        }

        [HarmonyPatch]
        private static class NativeCursor_WindowOverride
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return InputMethod(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture));
                yield return InputMethod(typeof(Menu), nameof(Menu.UpdateCursor));
                yield return InputMethod(typeof(FejdStartup), nameof(FejdStartup.UpdateCursor));
            }

            [HarmonyPriority(Priority.First)]
            private static bool Prefix()
            {
                if (!ManagerWindowOpen || !Application.isFocused)
                    return true;
                ApplyWindowCursor();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput), new[] { typeof(bool) })]
        private static class PlayerController_WindowInput
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref bool __result)
            {
                // Let FixedUpdate and LateUpdate run their native zero-controls/zero-look paths.
                if (CurrentInputPrevention != PreventInput.Off)
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
        private static class TextInput_WindowVisible
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref bool __result)
            {
                if (ManagerWindowOpen && CurrentInputPrevention != PreventInput.Off)
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(Console), nameof(Console.Update))]
        private static class Console_InputScope
        {
            [HarmonyPriority(Priority.First)]
            private static void Prefix(out int __state)
            {
                __state = _consoleInputScope;
                if (Console.IsVisible())
                    _consoleVisibleFrame = Time.frameCount;
                if (CurrentInputPrevention == PreventInput.Player)
                    _consoleInputScope++;
            }

            [HarmonyFinalizer]
            private static void Finalizer(int __state)
            {
                if (Console.IsVisible())
                    _consoleVisibleFrame = Time.frameCount;
                _consoleInputScope = __state;
            }
        }

        [HarmonyPatch]
        private static class ZInput_ButtonQueries
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return InputMethod(typeof(ZInput), nameof(ZInput.TryGetButtonState));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.TryGetKeyStateLowLevel));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetRadialTap));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetRadialMultiTap));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.HasDoubleTapped));
            }

            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ref bool __result)
            {
                // Do not block ShouldAcceptInputFromSource/OnActionCanceled: releases and device
                // switching must keep updating while gameplay consumers see neutral input.
                if (!BlockGameInput)
                    return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch]
        private static class ZInput_FloatQueries
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AnalogInputMethod(typeof(float));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetButtonPressedTimer));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetButtonLastPressedTimer));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetLongPressProgress));
            }

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref float __result)
            {
                if (BlockGameInput)
                    __result = 0f;
            }
        }

        [HarmonyPatch]
        private static class ZInput_VectorQueries
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AnalogInputMethod(typeof(Vector2));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetMouseDelta));
                yield return InputMethod(typeof(ZInput), nameof(ZInput.GetGyro));
            }

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref Vector2 __result)
            {
                if (BlockGameInput)
                    __result = Vector2.zero;
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetTouchPinchPoints))]
        private static class ZInput_TouchPinchQuery
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref List<Vector2> __result)
            {
                if (BlockGameInput)
                {
                    NoTouchPoints.Clear();
                    __result = NoTouchPoints;
                }
            }
        }

        [HarmonyPatch]
        private static class Inventory_WindowInput
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return InputMethod(typeof(InventoryGrid), nameof(InventoryGrid.OnLeftClick));
                yield return InputMethod(typeof(InventoryGrid), nameof(InventoryGrid.OnLeftDown));
                yield return InputMethod(typeof(InventoryGrid), nameof(InventoryGrid.OnRightDown));
                yield return InputMethod(typeof(InventoryGrid), nameof(InventoryGrid.EquipHovered));
                yield return InputMethod(typeof(InventoryGrid), nameof(InventoryGrid.OnBeginDrag));
                yield return InputMethod(typeof(InventoryGrid), nameof(InventoryGrid.OnReleasedOn));
                yield return InputMethod(typeof(InventoryGrid), nameof(InventoryGrid.OnDragEnd));
                yield return InputMethod(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem));
                yield return InputMethod(typeof(InventoryGui), nameof(InventoryGui.OnReleasedItem));
                yield return InputMethod(typeof(InventoryGui), nameof(InventoryGui.OnRightClickItem));
                yield return InputMethod(typeof(Player), nameof(Player.UseHotbarItem));
            }

            [HarmonyPriority(Priority.First)]
            private static bool Prefix() => CurrentInputPrevention == PreventInput.Off;
        }

        [HarmonyPatch(typeof(UIInputHandler), nameof(UIInputHandler.OnPointerUp))]
        private static class UIInputHandler_WindowPointerUp
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(UIInputHandler __instance)
            {
                if (AllowUIInput(__instance))
                    return true;
                // Preserve the known inventory pressed-item cleanup without dispatching other
                // pointer-up actions such as a map click below the configuration window.
                InventoryGrid grid = __instance.GetComponentInParent<InventoryGrid>();
                if (grid)
                    grid.OnLeftRelease(__instance);
                return false;
            }
        }

        [HarmonyPatch]
        private static class UI_WindowInput
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return InputMethod(typeof(UIInputHandler), nameof(UIInputHandler.OnPointerDown));
                yield return InputMethod(typeof(UIInputHandler), nameof(UIInputHandler.OnPointerClick));
                yield return InputMethod(typeof(UIDragHandler), nameof(UIDragHandler.OnBeginDrag));
                yield return InputMethod(typeof(UIDragHandler), nameof(UIDragHandler.OnDrag));
                yield return InputMethod(typeof(UIDragHandler), nameof(UIDragHandler.OnEndDrag));
                yield return InputMethod(typeof(UIDragHandler), nameof(UIDragHandler.OnReleasedOn));
                yield return InputMethod(typeof(Button), nameof(Button.OnPointerClick));
                yield return InputMethod(typeof(Button), nameof(Button.OnSubmit));
                yield return InputMethod(typeof(Button), "Press");
                yield return InputMethod(typeof(Toggle), nameof(Toggle.OnSubmit));
                yield return InputMethod(typeof(Toggle), nameof(Toggle.OnPointerClick));
                yield return InputMethod(typeof(Selectable), nameof(Selectable.OnPointerDown));
                yield return InputMethod(typeof(Selectable), nameof(Selectable.OnMove));
                yield return InputMethod(typeof(Slider), nameof(Slider.OnPointerDown));
                yield return InputMethod(typeof(Slider), nameof(Slider.OnDrag));
                yield return InputMethod(typeof(Slider), nameof(Slider.OnMove));
                yield return InputMethod(typeof(Scrollbar), nameof(Scrollbar.OnPointerDown));
                yield return InputMethod(typeof(Scrollbar), nameof(Scrollbar.OnDrag));
                yield return InputMethod(typeof(Scrollbar), nameof(Scrollbar.OnBeginDrag));
                yield return InputMethod(typeof(Scrollbar), nameof(Scrollbar.OnMove));
                yield return InputMethod(typeof(ScrollRect), nameof(ScrollRect.OnScroll));
                yield return InputMethod(typeof(ScrollRect), nameof(ScrollRect.OnBeginDrag));
                yield return InputMethod(typeof(ScrollRect), nameof(ScrollRect.OnDrag));
                yield return InputMethod(typeof(InputField), nameof(InputField.OnPointerClick));
                yield return InputMethod(typeof(InputField), nameof(InputField.OnUpdateSelected));
                yield return InputMethod(typeof(InputField), nameof(InputField.OnSubmit));
                yield return InputMethod(typeof(TMP_InputField), nameof(TMP_InputField.OnPointerClick));
                yield return InputMethod(typeof(TMP_InputField), nameof(TMP_InputField.OnUpdateSelected));
                yield return InputMethod(typeof(TMP_InputField), nameof(TMP_InputField.OnSubmit));
                yield return InputMethod(typeof(Dropdown), nameof(Dropdown.OnPointerClick));
                yield return InputMethod(typeof(Dropdown), nameof(Dropdown.OnSubmit));
                yield return InputMethod(typeof(TMP_Dropdown), nameof(TMP_Dropdown.OnPointerClick));
                yield return InputMethod(typeof(TMP_Dropdown), nameof(TMP_Dropdown.OnSubmit));
                // Native Selectable.OnPointerUp and ScrollRect.OnEndDrag must still release
                // uGUI pressed/dragging state. UIDragHandler, unlike ScrollRect, only dispatches
                // inventory callbacks and must not perform a drop while the window is open.
            }

            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Component __instance) => AllowUIInput(__instance);
        }
    }
}
