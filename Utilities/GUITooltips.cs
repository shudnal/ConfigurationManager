using System.Collections.Generic;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    /// <summary>
    /// Tracks tooltip hit areas in their owning IMGUI window, including disabled controls and
    /// nested scroll views. Each hit test uses Event.mousePosition in the current GUI group.
    /// </summary>
    internal static class GUITooltips
    {
        private static readonly List<string> HoveredTooltips = new List<string>();
        private static readonly Stack<int> ScrollStarts = new Stack<int>();
        private static bool _registeredHover;

        public static Rect WindowRect { get; private set; }

        public static void BeginWindow(Rect rect)
        {
            WindowRect = new Rect(Vector2.zero, rect.size);
            HoveredTooltips.Clear();
            ScrollStarts.Clear();
            _registeredHover = false;
            // Unity 6 can retain a tooltip across OnGUI passes/windows. Never reuse that state.
            GUI.tooltip = string.Empty;
        }

        public static void EndWindow()
        {
            GUI.tooltip = string.Empty;
            HoveredTooltips.Clear();
            ScrollStarts.Clear();
        }

        public static void Label(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            GUILayout.Label(content, style, options);
            RegisterLastRect(content);
        }

        public static bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            bool clicked = GUILayout.Button(content, style, options);
            RegisterLastRect(content);
            return clicked;
        }

        public static bool Toggle(bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            bool result = GUILayout.Toggle(value, content, style, options);
            RegisterLastRect(content);
            return result;
        }

        private static void RegisterLastRect(GUIContent content)
        {
            if (ComboBox.BlockWindowInput || ComboBox.IsShown() ||
                Event.current.type != EventType.Repaint || string.IsNullOrEmpty(content?.tooltip))
                return;
            if (!GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                return;
            _registeredHover = true;
            HoveredTooltips.Add(content.tooltip);
        }

        public static Vector2 BeginScrollView(Vector2 position, params GUILayoutOption[] options)
        {
            BeginScrollClip();
            return GUILayout.BeginScrollView(position, options);
        }

        public static Vector2 BeginScrollView(Vector2 position, bool horizontal, bool vertical, params GUILayoutOption[] options)
        {
            BeginScrollClip();
            return GUILayout.BeginScrollView(position, horizontal, vertical, options);
        }

        private static void BeginScrollClip()
        {
            if (Event.current.type == EventType.Repaint)
                ScrollStarts.Push(HoveredTooltips.Count);
        }

        public static void EndScrollView()
        {
            GUILayout.EndScrollView();
            if (Event.current.type != EventType.Repaint || ScrollStarts.Count == 0)
                return;

            int start = ScrollStarts.Pop();
            // GetLastRect is now the viewport in the parent group, not a scrolled child rect.
            // Checking after EndScrollView also works before the first cached layout exists.
            if (!GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                HoveredTooltips.RemoveRange(start, HoveredTooltips.Count - start);
        }

        public static string GetTooltip()
        {
            if (ComboBox.BlockWindowInput || ComboBox.IsShown() ||
                Event.current.type != EventType.Repaint || !Application.isFocused ||
                !WindowRect.Contains(Event.current.mousePosition))
                return null;
            if (HoveredTooltips.Count > 0)
                return HoveredTooltips[HoveredTooltips.Count - 1];
            // Retain native tooltip support for third-party custom drawers. An explicit tooltip
            // clipped by a scroll view must not leak back through the native fallback.
            return _registeredHover ? null : GUI.tooltip;
        }

        public static Rect GetVisibleWindowRect()
        {
            Vector2 screenMin = GUIUtility.ScreenToGUIPoint(Vector2.zero);
            Vector2 screenMax = GUIUtility.ScreenToGUIPoint(new Vector2(Screen.width, Screen.height));
            return Rect.MinMaxRect(Mathf.Max(0f, screenMin.x), Mathf.Max(0f, screenMin.y),
                Mathf.Min(WindowRect.width, screenMax.x), Mathf.Min(WindowRect.height, screenMax.y));
        }
    }
}
