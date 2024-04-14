// Based on code made by MarC0 / ManlyMarco https://github.com/BepInEx/BepInEx.ConfigurationManager on BepInEx version 5
// Copyright 2018 GNU General Public License v3.0
// Coloring and localization are based on aedenthorn's https://github.com/aedenthorn/BepInEx.ConfigurationManager
// Colors drawer is based on Azumatt https://github.com/AzumattDev/BepInEx.ConfigurationManager

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static ConfigurationManager.ConfigurationManagerStyles;

namespace ConfigurationManager
{
    [BepInPlugin(pluginID, pluginName, pluginVersion)]
    [BepInIncompatibility("com.bepis.bepinex.configurationmanager")]
    public class ConfigurationManager : BaseUnityPlugin
    {
        public const string pluginID = "shudnal.ConfigurationManager";
        public const string pluginName = "Valheim Configuration Manager";
        public const string pluginVersion = "1.0.1";

        public static void LogInfo(object data)
        {
            if (_loggingEnabled.Value)
                instance.Logger.LogInfo(data);
        }

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

        internal Rect DefaultWindowRect { get; private set; }
        internal Rect currentWindowRect;
        private Vector2 _settingWindowScrollPos;
        private bool _showDebug;

        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;
        private int _previousCursorLockState;
        private bool _previousCursorVisible;

        internal static Texture2D WindowBackground { get; private set; }
        internal static Texture2D EntryBackground { get; private set; }
        internal static Texture2D WidgetBackground { get; private set; }

        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }

        public static ConfigEntry<bool> _showAdvanced;
        public static ConfigEntry<bool> _showKeybinds;
        public static ConfigEntry<bool> _showSettings;
        public static ConfigEntry<bool> _loggingEnabled;
        public static ConfigEntry<bool> _preventInput;

        public static ConfigEntry<KeyboardShortcut> _keybind;
        public static ConfigEntry<bool> _hideSingleSection;
        public static ConfigEntry<bool> _pluginConfigCollapsedDefault;
        public static ConfigEntry<Vector2> _windowPosition;
        public static ConfigEntry<Vector2> _windowSize;

        public static ConfigEntry<string> _windowTitle;
        public static ConfigEntry<string> _normalText;
        public static ConfigEntry<string> _shortcutsText;
        public static ConfigEntry<string> _advancedText;
        public static ConfigEntry<string> _closeText;

        public static ConfigEntry<string> _searchText;
        public static ConfigEntry<string> _reloadText;
        public static ConfigEntry<string> _resetText;
        public static ConfigEntry<string> _resetSettingText;
        public static ConfigEntry<string> _expandText;
        public static ConfigEntry<string> _collapseText;
        public static ConfigEntry<string> _clearText;
        public static ConfigEntry<string> _cancelText;
        public static ConfigEntry<string> _enabledText; 
        public static ConfigEntry<string> _disabledText;
        public static ConfigEntry<string> _shortcutKeyText;
        public static ConfigEntry<string> _shortcutKeysText;

        public static ConfigEntry<int> _textSize;
        public static ConfigEntry<Color> _windowBackgroundColor;
        public static ConfigEntry<Color> _entryBackgroundColor;
        public static ConfigEntry<Color> _fontColor;
        public static ConfigEntry<Color> _fontColorValueChanged;
        public static ConfigEntry<Color> _fontColorValueDefault;
        public static ConfigEntry<Color> _widgetBackgroundColor;
        public static ConfigEntry<Color> _enabledBackgroundColor;

        private readonly Harmony harmony = new Harmony(pluginID);

        public void Awake()
        {
            instance = this;

            CalculateDefaultWindowRect();
            _fieldDrawer = new SettingFieldDrawer(this);

            _keybind = Config.Bind("General", "Show config manager", new KeyboardShortcut(KeyCode.F1),
                new ConfigDescription("The shortcut used to toggle the config manager window on and off.\n" +
                                      "The key can be overridden by a game-specific plugin if necessary, in that case this setting is ignored."));

            _showAdvanced = Config.Bind("Filtering", "Show advanced", false);
            _showKeybinds = Config.Bind("Filtering", "Show keybinds", true);
            _showSettings = Config.Bind("Filtering", "Show settings", true);

            _hideSingleSection = Config.Bind("General", "Hide single sections", false, new ConfigDescription("Show section title for plugins with only one section"));
            _loggingEnabled = Config.Bind("General", "Logging enabled", false, new ConfigDescription("Enable logging"));
            _preventInput = Config.Bind("General", "Prevent user input", true, new ConfigDescription("Prevent clicks and key presses when window is open"));

            _windowTitle = Config.Bind("Text - Menu", "Window Title", "Configuration Manager", new ConfigDescription("Window title text"));
            _normalText = Config.Bind("Text - Menu", "Normal", "Normal", new ConfigDescription("Normal settings toggle text"));
            _shortcutsText = Config.Bind("Text - Menu", "Shortcuts", "Keybinds", new ConfigDescription("Shortcut key settings toggle text"));
            _advancedText = Config.Bind("Text - Menu", "Advanced", "Advanced", new ConfigDescription("Advanced settings toggle text"));
            _closeText = Config.Bind("Text - Menu", "Close", "Close", new ConfigDescription("Advanced settings toggle text"));
            _searchText = Config.Bind("Text - Menu", "Search", "Search Settings:", new ConfigDescription("Search label text"));
            _expandText = Config.Bind("Text - Menu", "List Expand", "Expand", new ConfigDescription("Expand button text"));
            _collapseText = Config.Bind("Text - Menu", "List Collapse", "Collapse", new ConfigDescription("Collapse button text"));

            _reloadText = Config.Bind("Text - Plugin", "Reload", "Reload From File", new ConfigDescription("Reload mod config from file text"));
            _resetText = Config.Bind("Text - Plugin", "Reset", "Reset To Default", new ConfigDescription("Reset mod config to default text"));

            _resetSettingText = Config.Bind("Text - Config", "Setting Reset", "Reset", new ConfigDescription("Reset setting text"));
            _clearText = Config.Bind("Text - Config", "Setting Clear", "Clear", new ConfigDescription("Clear search text"));
            _cancelText = Config.Bind("Text - Config", "Setting Cancel", "Cancel", new ConfigDescription("Cancel button text"));
            _enabledText = Config.Bind("Text - Config", "Toggle True", "Enabled", new ConfigDescription("Text on enabled toggle"));
            _disabledText = Config.Bind("Text - Config", "Toggle False", "Disabled", new ConfigDescription("Text on disabled toggle"));
            _shortcutKeyText = Config.Bind("Text - Config", "Shortcut key single", "Set", new ConfigDescription("Text when waiting for key press"));
            _shortcutKeysText = Config.Bind("Text - Config", "Shortcut keys combination", "Press any key", new ConfigDescription("Text when waiting for key combination"));

            _pluginConfigCollapsedDefault = Config.Bind("General", "Plugin collapsed default", true, new ConfigDescription("If set to true plugins will be collapsed when opening the configuration manager window"));
            _windowPosition = Config.Bind("General", "Window position", new Vector2(55, 35), "Window position");
            _windowSize = Config.Bind("General", "Window size", DefaultWindowRect.size, "Window size");
            _textSize = Config.Bind("General", "Font size", 14, "Font size");

            _windowBackgroundColor = Config.Bind("Colors", "Window background color", new Color(0, 0, 0, 1), "Window background color");
            _entryBackgroundColor = Config.Bind("Colors", "Entry background color", new Color(0.55f, 0.5f, 0.5f, 0.87f), "Entry background color");
            _widgetBackgroundColor = Config.Bind("Colors", "Widget color", new Color(0.88f, 0.46f, 0, 0.8f), "Widget color");
            _enabledBackgroundColor = Config.Bind("Colors", "Enabled toggle color", new Color(0.88f, 0.46f, 0f, 1f), "Color of enabled toggle");

            _fontColor = Config.Bind("Colors - Font", "Main font", new Color(1f, 0.71f, 0.36f, 1f), "Font color");
            _fontColorValueDefault = Config.Bind("Colors - Font", "Default value", new Color(1f, 0.71f, 0.36f, 1f), "Font color");
            _fontColorValueChanged = Config.Bind("Colors - Font", "Changed value", new Color(0.9f, 0.9f, 0.9f, 1f), "Font color when value is not default");

            currentWindowRect = new Rect(_windowPosition.Value, _windowSize.Value);

            harmony.PatchAll();
        }

        void OnDestroy()
        {
            instance = null;
            harmony?.UnpatchSelf();
        }

        private void OnGUI()
        {
            if (DisplayingWindow)
            {
                if (_textSize.Value > 9 && _textSize.Value < 100)
                    fontSize = Mathf.Clamp(_textSize.Value, 10, 30);

                CreateBackgrounds();
                CreateStyles();
                SetUnlockCursor(0, true);

                GUI.Box(currentWindowRect, GUIContent.none, new GUIStyle());
                GUI.backgroundColor = _windowBackgroundColor.Value;

                if (_windowSize.Value.x > 200 && _windowSize.Value.x < Screen.width && _windowSize.Value.y > 200 && _windowSize.Value.y < Screen.height)
                    currentWindowRect.size = _windowSize.Value;

                RightColumnWidth = Mathf.RoundToInt(currentWindowRect.width / 2.5f * fontSize / 12f);
                LeftColumnWidth = Mathf.RoundToInt(currentWindowRect.width - RightColumnWidth - 115);

                currentWindowRect = GUILayout.Window(WindowId, currentWindowRect, SettingsWindow, _windowTitle.Value, GetWindowStyle());

                if (!UnityInput.Current.GetKey(KeyCode.Mouse0) && (currentWindowRect.x != _windowPosition.Value.x || currentWindowRect.y != _windowPosition.Value.y))
                {
                    _windowPosition.Value = currentWindowRect.position;
                    Config.Save();
                    SettingFieldDrawer.ClearComboboxCache();
                }
            }
        }

        private void Update()
        {
            if (DisplayingWindow) SetUnlockCursor(0, true);

            if (OverrideHotkey) return;

            if (!DisplayingWindow && _keybind.Value.IsDown())
                DisplayingWindow = true;
            else if (DisplayingWindow && (UnityInput.Current.GetKeyDown(KeyCode.Escape) || _keybind.Value.IsDown()))
                DisplayingWindow = false;
        }

        private void Start()
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

        private void SettingsWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, currentWindowRect.width, 20));
            DrawWindowHeader();

            _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, false, true);

            var scrollPosition = _settingWindowScrollPos.y;
            var scrollHeight = currentWindowRect.height;

            GUILayout.BeginVertical();
            {
                var currentHeight = 0;

                foreach (var plugin in _filteredSetings)
                {
                    var visible = plugin.Height == 0 || currentHeight + plugin.Height >= scrollPosition && currentHeight <= scrollPosition + scrollHeight;

                    if (visible)
                    {
                        try
                        {
                            DrawSinglePlugin(plugin);
                        }
                        catch (ArgumentException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                        }

                        if (plugin.Height == 0 && Event.current.type == EventType.Repaint)
                            plugin.Height = (int)GUILayoutUtility.GetLastRect().height;
                    }
                    else
                    {
                        try
                        {
                            GUILayout.Space(plugin.Height);
                        }
                        catch (ArgumentException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                        }
                    }

                    currentHeight += plugin.Height;
                }

                if (_showDebug)
                {
                    GUILayout.Space(10);
                    GUILayout.Label("Plugins with no options available: " + _modsWithoutSettings, GetLabelStyle());
                }
                else
                {
                    // Always leave some space in case there's a dropdown box at the very bottom of the list
                    GUILayout.Space(70);
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (!SettingFieldDrawer.DrawCurrentDropdown())
                DrawTooltip(currentWindowRect);
        }

        private void DrawWindowHeader()
        {
            GUI.backgroundColor = _entryBackgroundColor.Value;
            GUILayout.BeginHorizontal();
            {
                GUI.enabled = SearchString == string.Empty;

                var newVal = GUILayout.Toggle(_showSettings.Value, _normalText.Value, GetToggleStyle());
                if (_showSettings.Value != newVal)
                {
                    _showSettings.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUILayout.Toggle(_showKeybinds.Value, _shortcutsText.Value, GetToggleStyle());
                if (_showKeybinds.Value != newVal)
                {
                    _showKeybinds.Value = newVal;
                    BuildFilteredSettingList();
                }

                newVal = GUILayout.Toggle(_showAdvanced.Value, _advancedText.Value, GetToggleStyle());
                if (_showAdvanced.Value != newVal)
                {
                    _showAdvanced.Value = newVal;
                    BuildFilteredSettingList();
                }

                GUI.enabled = true;

                newVal = GUILayout.Toggle(_showDebug, "Debug mode", GetToggleStyle());
                if (_showDebug != newVal)
                {
                    _showDebug = newVal;
                    BuildSettingList();
                }

                if (GUILayout.Button(_closeText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    DisplayingWindow = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(_searchText.Value, GetLabelStyle(), GUILayout.ExpandWidth(false));

                GUI.SetNextControlName(SearchBoxName);
                SearchString = GUILayout.TextField(SearchString, GUILayout.ExpandWidth(true));

                if (_focusSearchBox)
                {
                    GUI.FocusWindow(WindowId);
                    GUI.FocusControl(SearchBoxName);
                    _focusSearchBox = false;
                }
                Color color = GUI.backgroundColor;
                GUI.backgroundColor = _widgetBackgroundColor.Value;
                if (GUILayout.Button(_clearText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    SearchString = string.Empty;
                GUI.backgroundColor = color;

                GUILayout.Space(8);

                if (GUILayout.Button(_pluginConfigCollapsedDefault.Value ? _expandText.Value : _collapseText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                {
                    var newValue = !_pluginConfigCollapsedDefault.Value;
                    _pluginConfigCollapsedDefault.Value = newValue;
                    foreach (var plugin in _filteredSetings)
                        plugin.Collapsed = newValue;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSinglePlugin(PluginSettingsData plugin)
        {
            GUI.backgroundColor = _entryBackgroundColor.Value;

            GUILayout.BeginVertical(GetBackgroundStyle());

            var categoryHeader = _showDebug ?
                new GUIContent(plugin.Info.Name.TrimStart('!') + " " + plugin.Info.Version, "GUID: " + plugin.Info.GUID) :
                new GUIContent(plugin.Info.Name.TrimStart('!') + " " + plugin.Info.Version);

            var isSearching = !string.IsNullOrEmpty(SearchString);

            if (SettingFieldDrawer.DrawPluginHeader(categoryHeader) && !isSearching)
                plugin.Collapsed = !plugin.Collapsed;

            if (isSearching || !plugin.Collapsed)
            {
                foreach (var category in plugin.Categories)
                {
                    if (!string.IsNullOrEmpty(category.Name))
                    {
                        if (plugin.Categories.Count > 1 || !_hideSingleSection.Value)
                            SettingFieldDrawer.DrawCategoryHeader(category.Name);
                    }

                    foreach (var setting in category.Settings)
                    {
                        DrawSingleSetting(setting);
                        GUILayout.Space(2);
                    }
                }
                GUILayout.BeginHorizontal();
                var color = GUI.backgroundColor;
                GUI.backgroundColor = _widgetBackgroundColor.Value;
                if (GUILayout.Button(_reloadText.Value, GetButtonStyle(), GUILayout.ExpandWidth(true)))
                {
                    foreach (var category in plugin.Categories)
                    {
                        foreach (var setting in category.Settings)
                        {
                            setting.PluginInstance.Config.Reload();
                            break;
                        }
                        break;
                    }
                    BuildFilteredSettingList();
                }
                if (GUILayout.Button(_resetText.Value, GetButtonStyle(), GUILayout.ExpandWidth(true)))
                {
                    foreach (var category in plugin.Categories)
                    {
                        foreach (var setting in category.Settings)
                        {
                            setting.Set(setting.DefaultValue);
                        }
                    }
                    BuildFilteredSettingList();
                }
                GUI.backgroundColor = color;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawSingleSetting(SettingEntryBase setting)
        {
            GUILayout.BeginHorizontal();
            {
                try
                {
                    DrawSettingName(setting);
                    _fieldDrawer.DrawSettingValue(setting);
                    DrawDefaultButton(setting);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, $"Failed to draw setting {setting.DispName} - {ex}");
                    GUILayout.Label("Failed to draw this field, check log for details.", GetLabelStyle());
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSettingName(SettingEntryBase setting)
        {
            if (setting.HideSettingName) return;

            GUILayout.Label(new GUIContent(setting.DispName.TrimStart('!'), setting.Description), GetLabelStyle(),
                GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth));
        }

        private static void DrawDefaultButton(SettingEntryBase setting)
        {
            if (setting.HideDefaultButton) return;

            GUI.backgroundColor = _widgetBackgroundColor.Value;

            bool DrawDefaultButton()
            {
                GUILayout.Space(5);
                return GUILayout.Button(_resetSettingText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false));
            }

            if (setting.DefaultValue != null)
            {
                if (DrawDefaultButton())
                    setting.Set(setting.DefaultValue);
            }
            else if (setting.SettingType.IsClass)
            {
                if (DrawDefaultButton())
                    setting.Set(null);
            }
        }

        /// <summary>
        /// Is the config manager main window displayed on screen
        /// </summary>
        public bool DisplayingWindow
        {
            get => _displayingWindow;
            set
            {
                if (_displayingWindow == value) return;
                _displayingWindow = value;

                SettingFieldDrawer.ClearCache();

                if (_displayingWindow)
                {
                    CalculateDefaultWindowRect();

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

        public void BuildSettingList()
        {
            SettingSearcher.CollectSettings(out var results, out var modsWithoutSettings, _showDebug);

            _modsWithoutSettings = string.Join(", ", modsWithoutSettings.Select(x => x.TrimStart('!')).OrderBy(x => x).ToArray());
            _allSettings = results.ToList();

            BuildFilteredSettingList();
        }

        private void BuildFilteredSettingList()
        {
            IEnumerable<SettingEntryBase> results = _allSettings;

            var searchStrings = SearchString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (searchStrings.Length > 0)
            {
                results = results.Where(x => ContainsSearchString(x, searchStrings));
            }
            else
            {
                if (!_showAdvanced.Value)
                    results = results.Where(x => x.IsAdvanced != true);
                if (!_showKeybinds.Value)
                    results = results.Where(x => !IsKeyboardShortcut(x));
                if (!_showSettings.Value)
                    results = results.Where(x => x.IsAdvanced == true || IsKeyboardShortcut(x));
            }

            const string shortcutsCatName = "Keyboard shortcuts";
            string GetCategory(SettingEntryBase eb)
            {

                return eb.Category;
            }

            var settingsAreCollapsed = _pluginConfigCollapsedDefault.Value;

            var nonDefaultCollpasingStateByPluginName = new HashSet<string>();
            foreach (var pluginSetting in _filteredSetings)
            {
                if (pluginSetting.Collapsed != settingsAreCollapsed)
                {
                    nonDefaultCollpasingStateByPluginName.Add(pluginSetting.Info.Name);
                }
            }

            _filteredSetings = results
                .GroupBy(x => x.PluginInfo)
                .Select(pluginSettings =>
                {
                    var categories = pluginSettings
                        .GroupBy(GetCategory)
                        .OrderBy(x => string.Equals(x.Key, shortcutsCatName, StringComparison.Ordinal))
                        .ThenBy(x => x.Key)
                        .Select(x => new PluginSettingsData.PluginSettingsGroupData { Name = x.Key, Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList() });
                    return new PluginSettingsData { Info = pluginSettings.Key, Categories = categories.ToList(), Collapsed = nonDefaultCollpasingStateByPluginName.Contains(pluginSettings.Key.Name) ? !settingsAreCollapsed : settingsAreCollapsed };
                })
                .OrderBy(x => x.Info.Name)
                .ToList();
        }

        private static bool IsKeyboardShortcut(SettingEntryBase x)
        {
            return x.SettingType == typeof(KeyboardShortcut);
        }

        private static bool ContainsSearchString(SettingEntryBase setting, string[] searchStrings)
        {
            var combinedSearchTarget = setting.PluginInfo.Name + "\n" +
                                       setting.PluginInfo.GUID + "\n" +
                                       setting.DispName + "\n" +
                                       setting.Category + "\n" +
                                       setting.Description + "\n" +
                                       setting.DefaultValue + "\n" +
                                       setting.Get();

            return searchStrings.All(s => combinedSearchTarget.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private void CalculateDefaultWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Screen.height < 800 ? Screen.height : 800;
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            DefaultWindowRect = new Rect(offsetX, offsetY, width, height);

            LeftColumnWidth = Mathf.RoundToInt(DefaultWindowRect.width / 2.5f);
            RightColumnWidth = (int)DefaultWindowRect.width - LeftColumnWidth - 115;
        }

        private static void DrawTooltip(Rect area)
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                var currentEvent = Event.current;

                var color = GUI.backgroundColor;
                GUI.backgroundColor = _entryBackgroundColor.Value;
                const int width = 400;
                var height = GetTooltipStyle().CalcHeight(new GUIContent(GUI.tooltip), 400) + 10;

                var x = currentEvent.mousePosition.x + width > area.width
                    ? area.width - width
                    : currentEvent.mousePosition.x;

                var y = currentEvent.mousePosition.y + 25 + height > area.height
                    ? currentEvent.mousePosition.y - height
                    : currentEvent.mousePosition.y + 25;

                GUI.Box(new Rect(x, y, width, height), GUI.tooltip, GetTooltipStyle());
                GUI.backgroundColor = color;
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

        private void CreateBackgrounds()
        {
            var background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            background.SetPixel(0, 0, _windowBackgroundColor.Value);
            background.Apply();
            WindowBackground = background;

            var entryBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            entryBackground.SetPixel(0, 0, _entryBackgroundColor.Value);
            entryBackground.Apply();
            EntryBackground = entryBackground;
        }

        private void LateUpdate()
        {
            if (DisplayingWindow) SetUnlockCursor(0, true);
        }

        private void SetUnlockCursor(int lockState, bool cursorVisible)
        {
            if (_curLockState != null)
            {
                if (_obsoleteCursor)
                    _curLockState.SetValue(null, Convert.ToBoolean(lockState), null);
                else
                    _curLockState.SetValue(null, lockState, null);

                _curVisible.SetValue(null, cursorVisible, null);
            }
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
            }

            public int Height { get; set; }
        }
    }
}
