// Popup list created by Eric Haines
// ComboBox Extended by Hyungseok Seo.(Jerry) sdragoon@nate.com
// this oop version of ComboBox is refactored by zhujiangbo jumbozhu@gmail.com
// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0
// Added default/changed value style, combobox entry style

using System;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    internal class ComboBox
    {
        private static bool forceToUnShow;
        private static int useControlID = -1;
        private static bool isShown;

        private readonly GUIStyle boxStyle;
        private readonly GUIStyle buttonStyleDefault;
        private readonly GUIStyle buttonStyleChanged;
        private bool isClickedComboButton;
        private readonly GUIContent[] listContent;
        private readonly GUIStyle listStyle;
        private readonly int _windowYmax;

        internal static bool IsShown()
        {
            return isShown;
        }

        public ComboBox(Rect rect, GUIContent buttonContent, GUIContent[] listContent, GUIContent defaultValue, GUIStyle buttonStyleDefault, GUIStyle buttonStyleChanged, GUIStyle boxStyle, GUIStyle listStyle, float windowYmax)
        {
            Rect = rect;
            ButtonContent = buttonContent;
            DefaultValue = defaultValue;
            this.listContent = listContent;
            this.buttonStyleDefault = buttonStyleDefault;
            this.buttonStyleChanged = buttonStyleChanged;
            this.boxStyle = boxStyle;
            this.listStyle = listStyle;
            _windowYmax = (int)windowYmax;
        }

        public Rect Rect { get; set; }

        public GUIContent ButtonContent { get; set; }
        
        public GUIContent DefaultValue { get; set; }

        public void Show(Action<int> onItemSelected)
        {
            isShown = false;
            if (forceToUnShow)
            {
                forceToUnShow = false;
                isClickedComboButton = false;
            }

            var done = false;
            var controlID = GUIUtility.GetControlID(FocusType.Passive);

            Vector2 currentMousePosition = Vector2.zero;
            if (Event.current.GetTypeForControl(controlID) == EventType.MouseUp)
            {
                if (isClickedComboButton)
                {
                    done = true;
                    currentMousePosition = Event.current.mousePosition;
                }
            }

            if (GUI.Button(Rect, ButtonContent, ButtonContent.ToString().Equals(DefaultValue.ToString()) ? buttonStyleDefault : buttonStyleChanged))
            {
                if (useControlID == -1)
                {
                    useControlID = controlID;
                    isClickedComboButton = false;
                }

                if (useControlID != controlID)
                {
                    forceToUnShow = true;
                    useControlID = controlID;
                }
                isClickedComboButton = true;
            }

            if (isClickedComboButton)
            {
                isShown = true;
                GUI.enabled = false;
                GUI.color = Color.white;

                var location = GUIUtility.GUIToScreenPoint(new Vector2(Rect.x, Rect.y + listStyle.CalcHeight(listContent[0], 1f) + 5f));
                var size = new Vector2(Rect.width, listStyle.CalcHeight(listContent[0], 1f) * listContent.Length);

                var innerRect = new Rect(0, 0, size.x, size.y);

                var outerRectScreen = new Rect(location.x, location.y, size.x, size.y);
                if (outerRectScreen.yMax > _windowYmax)
                {
                    outerRectScreen.height = _windowYmax - outerRectScreen.y;
                    outerRectScreen.width += 20;
                }

                if (currentMousePosition != Vector2.zero && outerRectScreen.Contains(GUIUtility.GUIToScreenPoint(currentMousePosition)))
                    done = false;

                CurrentDropdownDrawer = () =>
                {
                    GUI.enabled = true;

                    var scrpos = GUIUtility.ScreenToGUIPoint(location);
                    var outerRectLocal = new Rect(scrpos.x, scrpos.y, outerRectScreen.width, outerRectScreen.height);

                    GUI.Box(outerRectLocal, GUIContent.none, boxStyle);

                    _scrollPosition = GUI.BeginScrollView(outerRectLocal, _scrollPosition, innerRect, false, false);
                    {
                        const int initialSelectedItem = -1;
                        var newSelectedItemIndex = GUI.SelectionGrid(innerRect, initialSelectedItem, listContent, 1, listStyle);
                        if (newSelectedItemIndex != initialSelectedItem)
                        {
                            onItemSelected(newSelectedItemIndex);
                            isClickedComboButton = false;
                        }
                    }
                    GUI.EndScrollView(true);
                };
            }

            if (done)
                isClickedComboButton = false;
        }

        private Vector2 _scrollPosition = Vector2.zero;

        public static Action CurrentDropdownDrawer { get; set; }
    }
}