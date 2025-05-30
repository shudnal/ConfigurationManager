﻿using System.Collections.Generic;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    public static class GUIHelper
    {
        static readonly Stack<Color> _colorStack = new Stack<Color>();

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