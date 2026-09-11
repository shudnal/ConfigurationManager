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
        private static ComboBox _openBox;
        private static ComboBox _windowDropdown;
        private static int _windowId;
        private static Rect _windowRect;
        private static Vector2 _windowMousePosition;
        private static bool _backgroundHoverSuppressed;
        private static bool _windowEnabled;
        private static bool _ownsDropdownAtWindowStart;
        private static bool _inputDeferred;
        private static EventType _deferredInputType;
        private static int _pendingHotControlRelease;

        private GUIStyle boxStyle;
        private GUIStyle buttonStyleDefault;
        private GUIStyle buttonStyleChanged;
        private readonly GUIContent[] listContent;
        private GUIStyle listStyle;
        private int _ownerWindowId;
        private int _popupHotControl;
        private Rect _buttonRect;
        private Vector2 _buttonScreenMin;
        private Vector2 _buttonScreenMax;
        private bool _hasButtonGeometry;
        private bool _geometryPending;
        private Action<int> _onItemSelected;
        private int _styleRevision = -1;
        private float _measuredWidth = -1f;
        private float _measuredContentHeight;
        private float _otherMeasuredWidth = -1f;
        private float _otherMeasuredContentHeight;
        private Rect _dropdownRect;
        private Rect _contentRect;
        private Vector2 _scrollPosition;
        private bool _hasGeometry;

        public static bool BlockWindowInput { get; private set; }

        public static bool IsShown() => _openBox != null;
        internal bool IsOpen => _openBox == this;

        public ComboBox(Rect rect, GUIContent buttonContent, GUIContent[] listContent, GUIStyle listStyle, float windowYmax)
            : this(rect, buttonContent, listContent, listContent.Length == 0 ? null : listContent[0],
                GetButtonStyle(), GetButtonStyle(isDefaultValue: false), GetBoxStyle(), listStyle ?? GetComboBoxStyle(), windowYmax)
        {
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
            // Bounds come from the current owning window, not a constructor-time snapshot.
        }

        public Rect Rect { get; set; }
        public GUIContent ButtonContent { get; set; }
        public GUIContent DefaultValue { get; set; }

        public static void BeginWindow(int windowId, Rect windowRect)
        {
            ReleasePendingHotControl();
            _windowId = windowId;
            _windowRect = windowRect;
            _windowMousePosition = Event.current.mousePosition;
            _windowEnabled = GUI.enabled;
            _windowDropdown = null;
            BlockWindowInput = IsShown();
            _ownsDropdownAtWindowStart = _openBox != null && _openBox._ownerWindowId == windowId;
            _inputDeferred = BlockWindowInput && IsInputEvent(Event.current.type);
            if (_inputDeferred)
            {
                // Background controls must still allocate their usual layout/control IDs, but
                // must not consume a popup click or scroll before the overlay is processed.
                _deferredInputType = Event.current.type;
                Event.current.type = EventType.Ignore;
            }
            if (BlockWindowInput)
                SuppressBackgroundHover();
        }

        public static void EndWindow()
        {
            try
            {
                if (_inputDeferred)
                {
                    RestoreDeferredInput();
                    if (_ownsDropdownAtWindowStart || new Rect(Vector2.zero, _windowRect.size).Contains(_windowMousePosition))
                        ConsumeInput();
                }
            }
            finally
            {
                RestoreWindowMousePosition();
                GUI.enabled = _windowEnabled;
                _windowDropdown = null;
                BlockWindowInput = false;
            }
        }

        public void Show(Action<int> onItemSelected)
        {
            RefreshStyles();
            EventType eventType = Event.current.type;
            bool resolvedLayout = eventType == EventType.Repaint || eventType == EventType.MouseDown ||
                eventType == EventType.MouseUp || eventType == EventType.KeyDown;

            bool enabled = GUI.enabled;
            bool clicked;
            try
            {
                GUI.enabled = enabled && listContent.Length > 0;
                clicked = GUI.Button(Rect, ButtonContent,
                    string.Equals(ButtonContent?.text, DefaultValue?.text, StringComparison.Ordinal) ? buttonStyleDefault : buttonStyleChanged);
            }
            finally
            {
                GUI.enabled = enabled;
            }

            // Capture only an opening/open dropdown, not every closed enum field.
            // The button may have used the event, so use its pre-button event type and rect.
            // Convert the corners here while the owning scroll/group clip is still active.
            bool captureGeometry = resolvedLayout && (clicked || (IsOpen && eventType == EventType.Repaint)) &&
                Rect.width > 1f && Rect.height > 1f && listContent.Length > 0;
            if (captureGeometry)
            {
                _buttonScreenMin = GUIUtility.GUIToScreenPoint(Rect.min);
                _buttonScreenMax = GUIUtility.GUIToScreenPoint(Rect.max);
                _hasButtonGeometry = true;
                _geometryPending = true;
            }

            if (clicked && captureGeometry)
            {
                CloseAll();
                _openBox = this;
                _ownerWindowId = _windowId;
                _popupHotControl = 0;
                BlockWindowInput = true;
                GUIUtility.keyboardControl = 0;
            }

            if (_openBox != this || _ownerWindowId != _windowId)
                return;

            if (!enabled || !_hasButtonGeometry || listContent.Length == 0)
            {
                CloseAll();
                return;
            }

            _onItemSelected = onItemSelected;
            _windowDropdown = this;
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
            _measuredWidth = _otherMeasuredWidth = -1f;
        }

        private void UpdateGeometry()
        {
            _hasGeometry = false;
            _geometryPending = false;
            if (!_hasButtonGeometry)
                return;

            // This method runs at the window root, not at the button's nested scroll clip.
            // Convert both corners; GUIToScreenRect preserves size rather than scaling it.
            Vector2 min = GUIUtility.ScreenToGUIPoint(_buttonScreenMin);
            Vector2 max = GUIUtility.ScreenToGUIPoint(_buttonScreenMax);
            _buttonRect = UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (_buttonRect.width <= 1f || _buttonRect.height <= 1f)
                return;
            float scale = Mathf.Max(0.01f, ConfigurationManager.instance.scaleFactor);
            Rect bounds = UnityEngine.Rect.MinMaxRect(
                Mathf.Max(0f, -_windowRect.x), Mathf.Max(ConfigurationManager.HeaderSize, -_windowRect.y),
                Mathf.Min(_windowRect.width, Screen.width / scale - _windowRect.x),
                Mathf.Min(_windowRect.height, Screen.height / scale - _windowRect.y));
            if (bounds.width <= 0f || bounds.height <= 0f)
                return;

            const float gap = 2f;
            float below = Mathf.Max(0f, bounds.yMax - Mathf.Max(bounds.yMin, _buttonRect.yMax + gap));
            float above = Mathf.Max(0f, Mathf.Min(bounds.yMax, _buttonRect.yMin - gap) - bounds.yMin);
            float contentWidth = Mathf.Min(_buttonRect.width, bounds.width);
            float contentHeight = GetContentHeight(contentWidth);
            bool openAbove = contentHeight > below && above > below;
            float availableHeight = openAbove ? above : below;
            if (availableHeight <= 0f)
                return;

            bool needsScrollbar = contentHeight > availableHeight;
            float scrollbarWidth = needsScrollbar ? Mathf.Max(0f, GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left) : 0f;
            float width = Mathf.Min(contentWidth + scrollbarWidth, bounds.width);
            contentWidth = Mathf.Max(1f, width - scrollbarWidth);
            contentHeight = GetContentHeight(contentWidth);
            float height = Mathf.Min(contentHeight, availableHeight);
            float x = Mathf.Clamp(_buttonRect.x, bounds.xMin, bounds.xMax - width);
            float y = openAbove ? Mathf.Min(_buttonRect.yMin - gap, bounds.yMax) - height : Mathf.Max(_buttonRect.yMax + gap, bounds.yMin);
            _dropdownRect = new Rect(x, y, width, height);
            _contentRect = new Rect(0f, 0f, contentWidth, contentHeight);
            _scrollPosition.x = 0f;
            _scrollPosition.y = Mathf.Clamp(_scrollPosition.y, 0f, Mathf.Max(0f, contentHeight - height));
            _hasGeometry = true;
        }

        private float GetContentHeight(float width)
        {
            if (_measuredWidth == width)
                return _measuredContentHeight;

            if (_otherMeasuredWidth == width)
                return _otherMeasuredContentHeight;

            float rowHeight = listStyle.fixedHeight;
            if (rowHeight <= 0f)
            {
                rowHeight = 1f;
                foreach (GUIContent content in listContent)
                    rowHeight = Mathf.Max(rowHeight, listStyle.CalcHeight(content, Mathf.Max(1f, width)));
            }
            _otherMeasuredWidth = _measuredWidth;
            _otherMeasuredContentHeight = _measuredContentHeight;
            _measuredWidth = width;
            _measuredContentHeight = rowHeight * listContent.Length +
                Mathf.Max(listStyle.margin.top, listStyle.margin.bottom) * (listContent.Length - 1);
            return _measuredContentHeight;
        }

        public static bool DrawCurrentDropdown()
        {
            bool hadDropdown = BlockWindowInput || IsShown();
            bool deferred = _inputDeferred;
            bool consumeDeferredInput = deferred && _ownsDropdownAtWindowStart;
            RestoreWindowMousePosition();
            RestoreDeferredInput();
            try
            {
                if (_openBox != null && _openBox._ownerWindowId == _windowId)
                {
                    if (_windowDropdown == _openBox)
                    {
                        if (_openBox._geometryPending)
                            _openBox.UpdateGeometry();
                        if (_openBox._hasGeometry)
                            _openBox.DrawDropdown(_openBox._onItemSelected);
                        else
                            CloseAll();
                    }
                    else if (Event.current.type != EventType.Layout && Event.current.type != EventType.Used)
                        CloseAll(); // The setting was filtered out or its drawer disappeared.
                }
                else if (deferred && Event.current.type == EventType.MouseDown &&
                    new Rect(Vector2.zero, _windowRect.size).Contains(_windowMousePosition))
                {
                    CloseAll(); // A different manager window was clicked.
                    consumeDeferredInput = true;
                }
            }
            finally
            {
                if (consumeDeferredInput)
                    ConsumeInput();
                _windowDropdown = null;
                // Background decorations such as the resize handle are drawn after the popup.
                // Keep them unhovered for this pass, including the pass that closes the popup.
                if (hadDropdown)
                    SuppressBackgroundHover();
            }
            return hadDropdown;
        }

        private void DrawDropdown(Action<int> onItemSelected)
        {
            if (!_hasGeometry)
                return;

            Event current = Event.current;
            if ((current.type == EventType.MouseDown && !_dropdownRect.Contains(current.mousePosition)) ||
                (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape))
            {
                CloseAll();
                ConsumeInput();
                return;
            }

            bool enabled = GUI.enabled;
            Color color = GUI.color;
            int selected = -1;
            int previousHotControl = GUIUtility.hotControl;
            try
            {
                GUI.enabled = _windowEnabled;
                GUI.color = Color.white;
                GUI.Box(_dropdownRect, GUIContent.none, boxStyle);
                _scrollPosition = GUI.BeginScrollView(_dropdownRect, _scrollPosition, _contentRect, false, false);
                try
                {
                    selected = GUI.SelectionGrid(_contentRect, -1, listContent, 1, listStyle);
                }
                finally
                {
                    GUI.EndScrollView();
                }
                if (GUIUtility.hotControl == 0 || previousHotControl == 0 || _popupHotControl == GUIUtility.hotControl)
                    _popupHotControl = GUIUtility.hotControl;
            }
            finally
            {
                GUI.enabled = enabled;
                GUI.color = color;
            }

            if (selected >= 0)
            {
                CloseAll();
                onItemSelected(selected);
            }
        }

        public static void CloseAll()
        {
            if (_openBox != null && _openBox._popupHotControl != 0)
                _pendingHotControlRelease = _openBox._popupHotControl;
            _openBox = null;
            _windowDropdown = null;
        }

        internal static void ReleasePendingHotControl()
        {
            if (_pendingHotControlRelease != 0 && GUIUtility.hotControl == _pendingHotControlRelease)
                GUIUtility.hotControl = 0;
            _pendingHotControlRelease = 0;
        }

        private static void SuppressBackgroundHover()
        {
            if (_backgroundHoverSuppressed)
                return;

            // This changes only the current IMGUI event, never the physical/game cursor.
            // Keep a finite pointer outside the window's clip so nested groups and scroll views
            // can still translate it without infinities, NaNs, or oversized sentinel coordinates.
            Event.current.mousePosition = new Vector2(-Mathf.Max(1f, _windowRect.width) - 1f,
                -Mathf.Max(1f, _windowRect.height) - 1f);
            _backgroundHoverSuppressed = true;
            GUI.tooltip = string.Empty;
        }

        private static void RestoreWindowMousePosition()
        {
            if (!_backgroundHoverSuppressed)
                return;

            Event.current.mousePosition = _windowMousePosition;
            _backgroundHoverSuppressed = false;
        }

        private static void RestoreDeferredInput()
        {
            if (!_inputDeferred)
                return;
            Event.current.type = _deferredInputType;
            _inputDeferred = false;
        }

        private static bool IsInputEvent(EventType type) => type == EventType.MouseDown || type == EventType.MouseUp ||
            type == EventType.MouseDrag || type == EventType.MouseMove || type == EventType.ScrollWheel || type == EventType.ContextClick ||
            type == EventType.KeyDown || type == EventType.KeyUp || type == EventType.TouchDown ||
            type == EventType.TouchUp || type == EventType.TouchMove;

        private static void ConsumeInput()
        {
            if (IsInputEvent(Event.current.type))
                Event.current.Use();
        }
    }
}
