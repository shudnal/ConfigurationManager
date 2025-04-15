// Based on code made by MarC0 / ManlyMarco https://github.com/BepInEx/BepInEx.ConfigurationManager on BepInEx version 5
// Copyright 2018 GNU General Public License v3.0
// Coloring and localization are based on aedenthorn's https://github.com/aedenthorn/BepInEx.ConfigurationManager
// Colors drawer is based on Azumatt https://github.com/AzumattDev/BepInEx.ConfigurationManager

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ConfigurationManager
{
    [BepInPlugin(GUID, pluginName, Version)]
    [BepInIncompatibility("com.bepis.bepinex.configurationmanager")]
    public partial class ConfigurationManager : BaseUnityPlugin
    {
        public const string GUID = "_shudnal.ConfigurationManager";
        public const string pluginName = "Valheim Configuration Manager";
        public const string Version = "1.1.0";

        internal static ConfigurationManager instance;
        private static SettingFieldDrawer _fieldDrawer;

        private const int WindowId = -68;

        private const string SearchBoxName = "searchBox";
        private bool _focusSearchBox;
        private string _searchString = string.Empty;

        /// <summary>
        /// Event fired every time the manager window is shown or hidden.
        /// </summary>
        public event EventHandler<ValueChangedEventArgs<bool>> DisplayingWindowChanged;

        /// <summary>
        /// Disable the hotkey check used by config manager. If enabled you have to set <see cref="DisplayingWindow"/> to show the manager.
        /// </summary>
        public bool OverrideHotkey;

        private bool _displayingWindow;
        private bool _obsoleteCursor;

        private string _modsWithoutSettings;

        private List<SettingEntryBase> _allSettings;
        private List<PluginSettingsData> _filteredSetings = new List<PluginSettingsData>();

        public Rect DefaultWindowRect { get; private set; }
        public Rect currentWindowRect; 
        private Vector2 _settingWindowScrollPos;
        private Vector2 _settingWindowCategoriesScrollPos;

        #region Compat
        internal Rect SettingWindowRect
        {
            get => currentWindowRect;
            private set
            {
                currentWindowRect = value;
            }
        }
        private bool _windowWasMoved;
        private bool _showDebug;

        /// <summary>
        /// Window is visible and is blocking the whole screen. This is true until the user moves the window, which lets it run while user interacts with the game.
        /// </summary>
        public bool IsWindowFullscreen => DisplayingWindow;

        /// <summary>
        /// Window scale factor
        /// </summary>
        public float ScaleFactor => _scaleFactor.Value * (_useValheimGuiScaleFactor.Value ? GetScreenSizeFactor() : 1f);

        /// <summary>
        /// Screen width with scale factor
        /// </summary>
        public float ScreenWidth => Screen.width / ScaleFactor;

        /// <summary>
        /// Screen height with scale factor
        /// </summary>
        public float ScreenHeight => Screen.height / ScaleFactor;

        #endregion

        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;
        private int _previousCursorLockState;
        private bool _previousCursorVisible;

        internal static Texture2D WindowBackground { get; private set; }
        internal static Texture2D EntryBackground { get; private set; }
        internal static Texture2D TooltipBackground { get; private set; }
        internal static Texture2D HeaderBackground { get; private set; }
        internal static Texture2D HeaderBackgroundHover { get; private set; }

        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }
        internal int PluginListColumnWidth { get; private set; }
        internal int SettingsListColumnWidth { get; private set; }

        public enum ReadOnlyStyle
        {
            Ignored,
            Colored,
            Disabled,
            Hidden
        }

        public static ConfigEntry<bool> _showAdvanced;
        public static ConfigEntry<bool> _loggingEnabled;
        public static ConfigEntry<ReadOnlyStyle> _readOnlyStyle;

        public static ConfigEntry<KeyboardShortcut> _keybind;
        public static ConfigEntry<KeyboardShortcut> _keybindResetPosition;
        public static ConfigEntry<KeyboardShortcut> _keybindResetScale;
        public static ConfigEntry<bool> _hideSingleSection;
        public static ConfigEntry<bool> _pluginConfigCollapsedDefault;
        public static ConfigEntry<int> _textSize;
        public static ConfigEntry<bool> _orderPluginByGuid;
        public static ConfigEntry<int> _rangePrecision;
        public static ConfigEntry<int> _vectorPrecision;
        public static ConfigEntry<bool> _vectorDynamicPrecision;

        public static ConfigEntry<bool> _splitView;
        public static ConfigEntry<float> _scaleFactor;
        public static ConfigEntry<float> _splitViewListSize;
        public static ConfigEntry<float> _columnSeparatorPosition;
        public static ConfigEntry<Vector2> _windowPosition;
        public static ConfigEntry<Vector2> _windowSize;
        public static ConfigEntry<bool> _showTooltipBlock;

        public static ConfigEntry<bool> _sortCategoriesByName;
        public static ConfigEntry<bool> _categoriesCollapseable;
        public static ConfigEntry<bool> _categoriesCollapsedDefault;

        public static ConfigEntry<string> _windowTitle;
        public static ConfigEntry<string> _normalText;
        public static ConfigEntry<string> _shortcutsText;
        public static ConfigEntry<string> _advancedText;
        public static ConfigEntry<string> _advancedTextTooltip;
        public static ConfigEntry<string> _closeText;

        public static ConfigEntry<Vector2> _windowPositionTextEditor;
        public static ConfigEntry<Vector2> _windowSizeTextEditor;
        public static ConfigEntry<string> _searchTextEditor;
        public static ConfigEntry<string> _saveFileTextEditor;
        public static ConfigEntry<string> _windowTitleTextEditor;
        public static ConfigEntry<string> _editableExtensions;
        public static ConfigEntry<bool> _showEmptyFolders;
        public static ConfigEntry<bool> _hideModConfigs;

        public static ConfigEntry<string> _searchText;
        public static ConfigEntry<string> _resetSettingText;
        public static ConfigEntry<string> _clearText;
        public static ConfigEntry<string> _cancelText;
        public static ConfigEntry<string> _enabledText; 
        public static ConfigEntry<string> _disabledText;
        public static ConfigEntry<string> _shortcutKeyText;
        public static ConfigEntry<string> _shortcutKeysText;
        public static ConfigEntry<string> _noOptionsPluginsText;
        public static ConfigEntry<string> _toggleTextEditorText;
        public static ConfigEntry<string> _viewModeListViewText;
        public static ConfigEntry<string> _viewModeSplitViewText;

        public static ConfigEntry<Color> _windowBackgroundColor;
        public static ConfigEntry<Color> _tooltipBackgroundColor;
        public static ConfigEntry<Color> _headerBackgroundColor;
        public static ConfigEntry<Color> _headerBackgroundHoverColor;
        public static ConfigEntry<Color> _entryBackgroundColor;
        public static ConfigEntry<Color> _fontColor;
        public static ConfigEntry<Color> _fontColorValueChanged;
        public static ConfigEntry<Color> _fontColorValueDefault;
        public static ConfigEntry<Color> _widgetBackgroundColor;
        public static ConfigEntry<Color> _enabledBackgroundColor;
        public static ConfigEntry<Color> _readOnlyColor;

        internal static void LogInfo(object data)
        {
            if (_loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }

        internal static void LogWarning(object data)
        {
            if (_loggingEnabled.Value)
                instance.Logger.LogWarning(data);
        }

        internal static void LogError(object data)
        {
            if (_loggingEnabled.Value)
                instance.Logger.LogError(data);
        }

        private void Awake()
        {
            instance = this;

            _fieldDrawer = new SettingFieldDrawer(this);

            _keybind = Config.Bind("General", "Show config manager", new KeyboardShortcut(KeyCode.F1),
                "The shortcut used to toggle the config manager window on and off.\n" +
                                      "The key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored.");

            _hideSingleSection = Config.Bind("General", "Hide single sections", false, "Show section title for plugins with only one section");
            _loggingEnabled = Config.Bind("General", "Logging enabled", false, "Enable logging");
            _pluginConfigCollapsedDefault = Config.Bind("General", "Plugin collapsed default", true, "If set to true plugins will be collapsed when opening the configuration manager window");
            _textSize = Config.Bind("General", "Font size", 14, "Font size");
            _orderPluginByGuid = Config.Bind("General", "Order plugins by GUID", false, "Default order is by plugin name");
            _rangePrecision = Config.Bind("General", "Range field precision", 3, "Number of symbols after comma in floating-point numbers");
            _vectorPrecision = Config.Bind("General", "Vector field precision", 2, "Number of symbols after comma in vectors");
            _vectorDynamicPrecision = Config.Bind("General", "Vector field dynamic precision", true, "If every value in vector is integer .0 part will be omitted. Type \",\" or \".\" in vector field to enable precision back.");
            _keybindResetPosition = Config.Bind("General", "Reset position and size", new KeyboardShortcut(KeyCode.F1, KeyCode.LeftControl), "Set configuration manager window size and position to default values.");
            _keybindResetScale = Config.Bind("General", "Reset scale", new KeyboardShortcut(KeyCode.F1, KeyCode.LeftShift), "Set configuration manager window scale to default value.");

            _orderPluginByGuid.SettingChanged += (sender, args) => BuildSettingList();

            _splitView = Config.Bind("General - Window", "Split View", true, "If enabled - plugins will be shown in the left column and plugin settings will be shown in the right column.");
            _scaleFactor = Config.Bind("General - Window", "Scale factor", 1f, new ConfigDescription("Scale factor of configuration manager window. Triple click on configuration manager window title to reset scale.", new AcceptableValueRange<float>(0.5f, 2.5f)));
            _splitViewListSize = Config.Bind("General - Window", "Split View list relative size", 0.3f, new ConfigDescription("Relative size (percentage of window width) of split view plugin names list", new AcceptableValueRange<float>(0.1f, 0.5f)));
            _columnSeparatorPosition = Config.Bind("General - Window", "Setting name relative size", 0.4f, new ConfigDescription("Relative position of virtual line separating setting name from value", new AcceptableValueRange<float>(0.2f, 0.6f)));

            CalculateDefaultWindowRect();

            _windowPosition = Config.Bind("General - Window", "Window position", GetDefaultManagerWindowPosition(), "Window position. Double click on window title to reset position.");
            _windowSize = Config.Bind("General - Window", "Window size", GetDefaultManagerWindowSize(), "Window size. Double click on window title to reset size.");
            _showTooltipBlock = Config.Bind("General - Window", "Show ? next to config values", true, "Show hoverable block to get tooltip.");

            _editableExtensions = Config.Bind("General - File Editor", "Editable files", "json,yaml,yml,cfg", "Comma separated list of extensions");
            _hideModConfigs = Config.Bind("General - File Editor", "Hide mod configs", true, "Hide .cfg files with mod configurations generated by BepInEx" +
                                                                                                                "\nIt is meant to be edited in configuration manager main window." +
                                                                                                                "\nConfigurations from inactive mod will be loaded anyway");
            _showEmptyFolders = Config.Bind("General - File Editor", "Show empty folders", false, "Hide or show directories with no files");
            _windowPositionTextEditor = Config.Bind("General - File Editor", "Window position", GetDefaultTextEditorWindowPosition(), "Window position. Double click on window title to reset position.");
            _windowSizeTextEditor = Config.Bind("General - File Editor", "Window size", GetDefaultTextEditorWindowSize(), "Window size. Double click on window title to reset size.");

            _sortCategoriesByName = Config.Bind("General - Categories", "Sort by name", false, "If disabled, categories will be sorted in the order in which they were declared by the mod author.");
            _categoriesCollapseable = Config.Bind("General - Categories", "Collapsable categories", true, "Categories can be collapsed to reduce lagging and to ease scrolling.");
            _categoriesCollapsedDefault = Config.Bind("General - Categories", "Collapsed by default", true, "If set to true plugin categories will be collapsed by default if plugin has more than 20 categories." +
                                                                                                            "\nCategories with non default values will not be collapsed.");

            _sortCategoriesByName.SettingChanged += (sender, args) => BuildSettingList();
            _categoriesCollapseable.SettingChanged += (sender, args) => BuildSettingList();
            _categoriesCollapsedDefault.SettingChanged += (sender, args) => BuildSettingList();

            _showAdvanced = Config.Bind("Filtering", "Show advanced", false);
            _readOnlyStyle = Config.Bind("Filtering", "Style readonly entries", ReadOnlyStyle.Colored, "Entries marked as readonly are not available for change.");

            _readOnlyStyle.SettingChanged += (sender, args) => BuildSettingList();

            _windowTitle = Config.Bind("Text - Menu", "Window Title", "Configuration Manager", "Window title text");
            _normalText = Config.Bind("Text - Menu", "Normal", "Normal", "Normal settings toggle text");
            _shortcutsText = Config.Bind("Text - Menu", "Shortcuts", "Keybinds", "Shortcut key settings toggle text");
            _advancedText = Config.Bind("Text - Menu", "Advanced", "Advanced", "Advanced settings toggle text");
            _advancedTextTooltip = Config.Bind("Text - Menu", "Advanced tooltip", "Show plugins and settings marked by author as advanced." +
                "\nFor example BepInEx settings are marked as advanced.", "Advanced settings toggle tooltip");
            _closeText = Config.Bind("Text - Menu", "Close", "Close", "Close button text");
            _searchText = Config.Bind("Text - Menu", "Search placeholder", "Search settings", "Search placeholder text");
            _noOptionsPluginsText = Config.Bind("Text - Menu", "Plugins without options", "Plugins with no options available", "Text in footer");
            _viewModeListViewText = Config.Bind("Text - Menu", "List View", "List View", "Text for button to change to single column legacy view mode");
            _viewModeSplitViewText = Config.Bind("Text - Menu", "Split View", "Split View", "Text for button to change to split view mode");

            _toggleTextEditorText = Config.Bind("File Editor - Text", "Open button", "Show File Editor", "Open file editor label text");
            _searchTextEditor = Config.Bind("File Editor - Text", "Search", "Search:", "Search label text");
            _saveFileTextEditor = Config.Bind("File Editor - Text", "Save", "Save", "Save changes in file");
            _windowTitleTextEditor = Config.Bind("File Editor - Text", "Title", "Configuration Files Editor", "Window title");

            _resetSettingText = Config.Bind("Text - Config", "Setting Reset", "Reset", "Reset setting text");
            _clearText = Config.Bind("Text - Config", "Setting Clear", "Clear", "Clear search text");
            _cancelText = Config.Bind("Text - Config", "Setting Cancel", "Cancel", "Cancel button text");
            _enabledText = Config.Bind("Text - Config", "Toggle True", "Enabled", "Text on enabled toggle");
            _disabledText = Config.Bind("Text - Config", "Toggle False", "Disabled", "Text on disabled toggle");
            _shortcutKeyText = Config.Bind("Text - Config", "Shortcut key single", "Set", "Text when waiting for key press");
            _shortcutKeysText = Config.Bind("Text - Config", "Shortcut keys combination", "Press any key", "Text when waiting for key combination");

            _windowBackgroundColor = Config.Bind("Colors", "Window background color", new Color(0, 0, 0, 1), "Window background color");
            _entryBackgroundColor = Config.Bind("Colors", "Entry background color", new Color(0.55f, 0.5f, 0.5f, 0.84f), "Entry background color");
            _tooltipBackgroundColor = Config.Bind("Colors", "Tooltip background color", new Color(0.55f, 0.5f, 0.45f, 0.95f), "Tooltip background color");
            _headerBackgroundColor = Config.Bind("Colors", "Header background color", new Color(0.74f, 0.54f, 0.37f, 0.8f), "Header background color");
            _headerBackgroundHoverColor = Config.Bind("Colors", "Header hover background color", new Color(0.88f, 0.46f, 0f, 0.8f), "Header hover background color");
            _widgetBackgroundColor = Config.Bind("Colors", "Widget color", new Color(0.88f, 0.46f, 0, 0.8f), "Widget color");
            _enabledBackgroundColor = Config.Bind("Colors", "Enabled toggle color", new Color(0.88f, 0.46f, 0f, 1f), "Color of enabled toggle");
            _readOnlyColor = Config.Bind("Colors", "Readonly color", Color.gray, "Color of readonly setting");

            _windowBackgroundColor.SettingChanged += (s, e) => UpdateBackgrounds();
            _entryBackgroundColor.SettingChanged += (s, e) => UpdateBackgrounds();
            _tooltipBackgroundColor.SettingChanged += (s, e) => UpdateBackgrounds();
            _headerBackgroundColor.SettingChanged += (s, e) => UpdateBackgrounds();
            _headerBackgroundHoverColor.SettingChanged += (s, e) => UpdateBackgrounds();

            _fontColor = Config.Bind("Colors - Font", "Main font", new Color(1f, 0.71f, 0.36f, 1f), "Font color");
            _fontColorValueDefault = Config.Bind("Colors - Font", "Default value", new Color(1f, 0.71f, 0.36f, 1f), "Font color");
            _fontColorValueChanged = Config.Bind("Colors - Font", "Changed value", new Color(0.9f, 0.9f, 0.9f, 1f), "Font color when value is not default");

            currentWindowRect = new Rect(_windowPosition.Value, _windowSize.Value);
        }

        private Vector2 GetDefaultManagerWindowPosition() => DefaultWindowRect.position;
        private Vector2 GetDefaultManagerWindowSize() => DefaultWindowRect.size;

        private Vector2 GetDefaultTextEditorWindowPosition() => new Vector2(GetDefaultManagerWindowPosition().x + GetDefaultManagerWindowSize().x + 35f, GetDefaultManagerWindowPosition().y);
        private Vector2 GetDefaultTextEditorWindowSize() => new Vector2(Screen.width - GetDefaultTextEditorWindowPosition().x - GetDefaultManagerWindowPosition().x, GetDefaultManagerWindowSize().y + GetDefaultManagerWindowPosition().y);
        
        void OnDestroy()
        {
            instance = null;
        }

        void Start()
        {
            // Use reflection to keep compatibility with unity 4.x since it doesn't have Cursor
            var tCursor = typeof(Cursor);
            _curLockState = tCursor.GetProperty("lockState", BindingFlags.Static | BindingFlags.Public);
            _curVisible = tCursor.GetProperty("visible", BindingFlags.Static | BindingFlags.Public);

            if (_curLockState == null && _curVisible == null)
            {
                _obsoleteCursor = true;

                _curLockState = typeof(Screen).GetProperty("lockCursor", BindingFlags.Static | BindingFlags.Public);
                _curVisible = typeof(Screen).GetProperty("showCursor", BindingFlags.Static | BindingFlags.Public);
            }

            // Check if user has permissions to write config files to disk
            try { Config.Save(); }
            catch (IOException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Failed to write to config directory, expect issues!\nError message:" + ex.Message); }
            catch (UnauthorizedAccessException ex) { Logger.Log(LogLevel.Message | LogLevel.Warning, "WARNING: Permission denied to write to config directory, expect issues!\nError message:" + ex.Message); }
        }

        void Update()
        {
            if (DisplayingWindow)
                SetUnlockCursor(0, true);

            if (OverrideHotkey)
                return;

            if (_keybindResetPosition.Value.IsDown())
                ResetWindowSizeAndPosition();

            if (_keybindResetScale.Value.IsDown())
                ResetWindowScale();

            if (!DisplayingWindow && _keybind.Value.IsDown())
                DisplayingWindow = true;
            else if (DisplayingWindow && (UnityInput.Current.GetKeyDown(KeyCode.Escape) || _keybind.Value.IsDown()))
                DisplayingWindow = false;
        }

        void LateUpdate()
        {
            if (DisplayingWindow)
                SetUnlockCursor(0, true);
        }

        /// <summary>
        /// Is the config manager main window displayed on screen
        /// </summary>
        public bool DisplayingWindow
        {
            get => _displayingWindow;
            set
            {
                if (_displayingWindow == value)
                    return;

                _displayingWindow = value;

                SettingFieldDrawer.ClearCache();

                CreateBackgrounds();

                if (_displayingWindow)
                {
                    BuildSettingList();

                    _focusSearchBox = false;

                    // Do through reflection for unity 4 compat
                    if (_curLockState != null)
                    {
                        _previousCursorLockState = _obsoleteCursor ? Convert.ToInt32((bool)_curLockState.GetValue(null, null)) : (int)_curLockState.GetValue(null, null);
                        _previousCursorVisible = (bool)_curVisible.GetValue(null, null);
                    }
                }
                else
                {
                    if (!_previousCursorVisible || _previousCursorLockState != 0) // 0 = CursorLockMode.None
                        SetUnlockCursor(_previousCursorLockState, _previousCursorVisible);
                }

                DisplayingWindowChanged?.Invoke(this, new ValueChangedEventArgs<bool>(value));
            }
        }

        /// <summary>
        /// String currently entered into the search box
        /// </summary>
        public string SearchString
        {
            get => _searchString;
            private set
            {
                if (value == null)
                    value = string.Empty;

                if (_searchString == value)
                    return;

                _searchString = value;

                BuildFilteredSettingList();
            }
        }

        /// <summary>
        /// Register a custom setting drawer for a given type. The action is ran in OnGui in a single setting slot.
        /// Do not use any Begin / End layout methods, and avoid raising height from standard.
        /// </summary>
        public static void RegisterCustomSettingDrawer(Type settingType, Action<SettingEntryBase> onGuiDrawer)
        {
            if (settingType == null) throw new ArgumentNullException(nameof(settingType));
            if (onGuiDrawer == null) throw new ArgumentNullException(nameof(onGuiDrawer));

            if (SettingFieldDrawer.SettingDrawHandlers.ContainsKey(settingType))
                LogInfo("Tried to add a setting drawer for type " + settingType.FullName + " while one already exists.");
            else
                SettingFieldDrawer.SettingDrawHandlers[settingType] = onGuiDrawer;
        }

        private sealed class PluginSettingsData
        {
            public BepInPlugin Info;
            public List<PluginSettingsGroupData> Categories;
            private bool _collapsed;

            public bool Collapsed
            {
                get => _collapsed;
                set
                {
                    _collapsed = value;
                    Height = 0;
                }
            }

            public sealed class PluginSettingsGroupData
            {
                public string Name;
                public List<SettingEntryBase> Settings;
                public bool Collapsed;
            }

            public int Height { get; set; }
        }
    }
}
