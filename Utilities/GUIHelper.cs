using System.Collections.Generic;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    public static class GUIHelper
    {
        static readonly Stack<Color> _colorStack = new Stack<Color>();

        internal static readonly GUILayoutOption ExpandWidthOption = GUILayout.ExpandWidth(true);
        internal static readonly GUILayoutOption FixedWidthOption = GUILayout.ExpandWidth(false);
        internal static readonly GUILayoutOption FixedHeightOption = GUILayout.ExpandHeight(false);
        internal static readonly GUILayoutOption[] ExpandWidth = { ExpandWidthOption };
        internal static readonly GUILayoutOption[] FixedWidth = { FixedWidthOption };
        internal static readonly GUILayoutOption[] FixedHeight = { FixedHeightOption };

        internal static void UpdateContent(GUIContent content, string text, string tooltip = null)
        {
            text = text ?? string.Empty;
            tooltip = tooltip ?? string.Empty;
            if (content.text != text)
                content.text = text;
            if (content.tooltip != tooltip)
                content.tooltip = tooltip;
        }

        public static void BeginColor(Color color)
        {
            _colorStack.Push(GUI.color);
            GUI.color = color;
            _colorStack.Push(GUI.backgroundColor);
            GUI.backgroundColor = Color.clear;
        }

        public static void EndColor()
        {
            GUI.backgroundColor = _colorStack.Pop();
            GUI.color = _colorStack.Pop();
        }

        public static bool IsEnterPressed()
        {
            return
                Event.current.isKey
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
        }
    }
}
