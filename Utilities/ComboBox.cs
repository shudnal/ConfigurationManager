// Popup list created by Eric Haines
// ComboBox Extended by Hyungseok Seo.(Jerry) sdragoon@nate.com
// this oop version of ComboBox is refactored by zhujiangbo jumbozhu@gmail.com
// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0
// Added default/changed value style, combobox entry style

using System;
using UnityEngine;
using static ConfigurationManager.ConfigurationManagerStyles;

namespace ConfigurationManager.Utilities
{
    internal class ComboBox
    {
        private static bool forceToUnShow;
        private static int useControlID = -1;
        private static ComboBox _openBox;
        private static int _ownerWindowId = int.MinValue;
        private static int _currentWindowId = int.MinValue;
        private static bool _inputDeferred;
        private static EventType _deferredInputType;

        private GUIStyle boxStyle;
        private GUIStyle buttonStyleDefault;
        private GUIStyle buttonStyleChanged;
        private bool isClickedComboButton;
        private readonly GUIContent[] listContent;
        private GUIStyle listStyle;
        private readonly int _windowYmax;
        private int _styleRevision = -1;

        public static bool IsShown() => _openBox != null && _openBox.isClickedComboButton;

        internal bool IsOpen => ReferenceEquals(_openBox, this) && isClickedComboButton;

        public ComboBox(Rect rect, GUIContent buttonContent, GUIContent[] listContent, GUIStyle listStyle, float windowYmax)
        {
            Rect = rect;
            ButtonContent = buttonContent;
            this.listContent = listContent;
            _windowYmax = (int)windowYmax;

            DefaultValue = listContent.Length == 0 ? null : listContent[0];
            buttonStyleDefault = GetButtonStyle();
            buttonStyleChanged = GetButtonStyle(isDefaultValue: false);
            boxStyle = GetBoxStyle();
            this.listStyle = listStyle ?? GetComboBoxStyle();
            _styleRevision = Revision;
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
            _styleRevision = Revision;
        }

        public Rect Rect { get; set; }

        public GUIContent ButtonContent { get; set; }

        public GUIContent DefaultValue { get; set; }

        public static void BeginWindow(int windowId)
        {
            _currentWindowId = windowId;
            _inputDeferred = IsShown() && IsPointerInput(Event.current.type);
            if (_inputDeferred)
            {
                _deferredInputType = Event.current.type;
                Event.current.type = EventType.Ignore;
            }
        }

        public static void EndWindow()
        {
            // Restore input for a later manager window unless the owner already consumed it.
            RestoreDeferredInput();
            _inputDeferred = false;
        }

        public static bool DrawCurrentDropdown()
        {
            bool ownerWindow = _ownerWindowId == _currentWindowId;
            bool hadDropdown = ownerWindow && (IsShown() || CurrentDropdownDrawer != null);
            if (!ownerWindow)
                return false;

            RestoreDeferredInput();

            if (CurrentDropdownDrawer != null)
            {
                CurrentDropdownDrawer.Invoke();
                CurrentDropdownDrawer = null;
            }
            else if (IsShown() && Event.current.type != EventType.Layout && Event.current.type != EventType.Used)
            {
                // The setting that owned the popup disappeared from the current GUI tree.
                CloseAll();
            }

            if (_inputDeferred && IsPointerInput(Event.current.type))
                Event.current.Use();

            _inputDeferred = false;
            if (!IsShown())
                _ownerWindowId = int.MinValue;

            return hadDropdown;
        }

        public static void CloseAll()
        {
            if (_openBox != null)
                _openBox.isClickedComboButton = false;

            _openBox = null;
            _ownerWindowId = int.MinValue;
            CurrentDropdownDrawer = null;
            forceToUnShow = false;
            useControlID = -1;
        }

        public void Show(Action<int> onItemSelected)
        {
            RefreshStyles();

            bool resumeInput = IsOpen && _inputDeferred && Event.current.type == EventType.Ignore;
            if (resumeInput)
                Event.current.type = _deferredInputType;

            try
            {
                ShowOriginal(onItemSelected);
            }
            finally
            {
                if (resumeInput && Event.current.type != EventType.Used)
                    Event.current.type = EventType.Ignore;
            }
        }

        private void ShowOriginal(Action<int> onItemSelected)
        {
            if (forceToUnShow)
            {
                forceToUnShow = false;
                isClickedComboButton = false;
                if (ReferenceEquals(_openBox, this))
                    _openBox = null;
            }

            if (listContent.Length == 0)
                return;

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

            if (GUI.Button(Rect, ButtonContent,
                    string.Equals(ButtonContent?.text, DefaultValue?.text, StringComparison.Ordinal)
                        ? buttonStyleDefault
                        : buttonStyleChanged))
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
                _openBox = this;
                _ownerWindowId = _currentWindowId;
            }

            if (isClickedComboButton)
            {
                // Keep the proven pre-1.1.18 coordinate calculation intact. GUIScale temporarily
                // removes Configuration Manager's GUI.matrix scale while these conversions run.
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
                    var enabled = GUI.enabled;
                    var color = GUI.color;
                    GUI.enabled = true;
                    GUI.color = Color.white;

                    var scrpos = GUIUtility.ScreenToGUIPoint(location);
                    var outerRectLocal = new Rect(scrpos.x, scrpos.y, outerRectScreen.width, outerRectScreen.height);

                    GUI.Box(outerRectLocal, GUIContent.none, boxStyle);

                    _scrollPosition = GUI.BeginScrollView(outerRectLocal, _scrollPosition, innerRect, false, false);
                    try
                    {
                        const int initialSelectedItem = -1;
                        var newSelectedItemIndex = GUI.SelectionGrid(innerRect, initialSelectedItem, listContent, 1, listStyle);
                        if (newSelectedItemIndex != initialSelectedItem)
                        {
                            onItemSelected(newSelectedItemIndex);
                            isClickedComboButton = false;
                            if (ReferenceEquals(_openBox, this))
                                _openBox = null;
                        }
                    }
                    finally
                    {
                        GUI.EndScrollView();
                    }

                    GUI.enabled = enabled;
                    GUI.color = color;
                };
            }

            if (done)
            {
                isClickedComboButton = false;
                if (ReferenceEquals(_openBox, this))
                    _openBox = null;
                CurrentDropdownDrawer = null;
            }
        }

        private static bool IsPointerInput(EventType type)
        {
            return type == EventType.MouseDown || type == EventType.MouseUp || type == EventType.MouseDrag ||
                   type == EventType.ScrollWheel || type == EventType.ContextClick || type == EventType.TouchDown ||
                   type == EventType.TouchUp || type == EventType.TouchMove;
        }

        private static void RestoreDeferredInput()
        {
            if (_inputDeferred && Event.current.type == EventType.Ignore)
                Event.current.type = _deferredInputType;
        }

        private void RefreshStyles()
        {
            if (_styleRevision == Revision)
                return;

            buttonStyleDefault = GetButtonStyle();
            buttonStyleChanged = GetButtonStyle(isDefaultValue: false);
            boxStyle = GetBoxStyle();
            listStyle = GetComboBoxStyle();
            _styleRevision = Revision;
        }

        private Vector2 _scrollPosition = Vector2.zero;

        public static Action CurrentDropdownDrawer { get; set; }
    }
}
