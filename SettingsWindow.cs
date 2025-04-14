using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ConfigurationManager.ConfigurationManagerStyles;

namespace ConfigurationManager
{
    public partial class ConfigurationManager
    {
        internal const int _headerSize = 20;

        internal float scaleFactor;
        internal Matrix4x4 guiMatrix;

        private float lastClickTime;
        private Vector2 lastClickPosition;
        private const float doubleClickThreshold = 0.3f;

        private ConfigFilesEditor configFilesEditor;

        public bool SplitView
        {
            get => _splitView == null ? true : _splitView.Value;
            set
            {
                if (_splitView.Value == (_splitView.Value = value))
                    return;

                if (_splitView.Value)
                {
                    _filteredSetings.Where(plg => !plg.Collapsed).Skip(1).Do(plg => plg.Collapsed = true);
                    if (_filteredSetings.All(plg => plg.Collapsed) && _filteredSetings.FirstOrDefault() is PluginSettingsData plugin)
                        plugin.Collapsed = false;
                }
            }
        }

        void OnGUI()
        {
            if (DisplayingWindow)
            {
                CreateStyles();
                SetUnlockCursor(0, true);

                if (scaleFactor != (scaleFactor = ScaleFactor))
                    guiMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleFactor, scaleFactor, 1f));

                currentWindowRect.size = _windowSize.Value;
                currentWindowRect.position = _windowPosition.Value;

                Matrix4x4 originalMatrix = GUI.matrix;
                GUI.matrix = guiMatrix;

                GUI.Box(currentWindowRect, GUIContent.none, new GUIStyle());
                Color color = GUI.backgroundColor;
                GUI.backgroundColor = _windowBackgroundColor.Value;

                CalculateSettingsColumnsWidth(currentWindowRect.width);

                currentWindowRect = GUILayout.Window(WindowId, currentWindowRect, SettingsWindow, _windowTitle.Value, GetWindowStyle());

                if (!UnityInput.Current.GetKeyDown(KeyCode.Mouse0) && (currentWindowRect.position != _windowPosition.Value))
                    SaveCurrentSizeAndPosition();

                GUI.backgroundColor = color;

                if (configFilesEditor == null)
                    configFilesEditor = new ConfigFilesEditor();

                configFilesEditor.OnGUI();

                GUI.matrix = originalMatrix;
            }
        }

        private void CalculateSettingsColumnsWidth(float width)
        {
            PluginListColumnWidth = Mathf.RoundToInt(width * _splitViewListSize.Value);
            SettingsListColumnWidth = Mathf.RoundToInt(SplitView ? width - PluginListColumnWidth : width);

            LeftColumnWidth = Mathf.RoundToInt(Mathf.Clamp(SettingsListColumnWidth * _columnSeparatorPosition.Value, width * 0.1f, width * 0.8f)) - 18;
            RightColumnWidth = Mathf.RoundToInt(Mathf.Clamp(SettingsListColumnWidth - LeftColumnWidth - 100, width * 0.2f, width * 0.8f));
        }

        internal void SaveCurrentSizeAndPosition()
        {
            _windowSize.Value = new Vector2(Mathf.Clamp(currentWindowRect.size.x, 500f, ScreenWidth), Mathf.Clamp(currentWindowRect.size.y, 200f, ScreenHeight));
            _windowPosition.Value = new Vector2(Mathf.Clamp(currentWindowRect.position.x, 0f, ScreenWidth - _windowSize.Value.x / 4f), Mathf.Clamp(currentWindowRect.position.y, 0f, ScreenHeight - _headerSize * 2));
            Config.Save();
            SettingFieldDrawer.ClearComboboxCache();
        }

        internal void ResetWindowSizeAndPosition()
        {
            _scaleFactor.Value = (float)_scaleFactor.DefaultValue;
            _splitViewListSize.Value = (float)_splitViewListSize.DefaultValue;
            _columnSeparatorPosition.Value = (float)_columnSeparatorPosition.DefaultValue;

            CalculateDefaultWindowRect();

            _windowSize.Value = GetDefaultManagerWindowSize();
            _windowPosition.Value = GetDefaultManagerWindowPosition();
            _windowSizeTextEditor.Value = GetDefaultTextEditorWindowSize();
            _windowPositionTextEditor.Value = GetDefaultTextEditorWindowPosition();
            Config.Save();
            SettingFieldDrawer.ClearComboboxCache();
        }

        private void HandleHeaderDblClick(Rect titleBarRect)
        {
            if (UnityInput.Current.GetMouseButtonDown(0) && titleBarRect.Contains(Event.current.mousePosition))
            {
                if (lastClickPosition == Event.current.mousePosition && Time.fixedTime != lastClickTime && Time.fixedTime - lastClickTime < doubleClickThreshold)
                    ResetWindowSizeAndPosition();

                lastClickTime = Time.fixedTime;
                lastClickPosition = Event.current.mousePosition;
            }
        }

        private void SettingsWindow(int id)
        {
            Rect headerRect = new Rect(0, 0, currentWindowRect.width, _headerSize);
            HandleHeaderDblClick(headerRect);

            GUI.DragWindow(headerRect);
            DrawWindowHeader();

            if (SplitView)
                DrawSplitView();
            else
                DrawSingleColumn();

            if (!SettingFieldDrawer.DrawCurrentDropdown())
                DrawTooltip(currentWindowRect);

            currentWindowRect = Utilities.Utils.ResizeWindow(id, currentWindowRect, out bool sizeChanged);

            if (sizeChanged)
                SaveCurrentSizeAndPosition();
        }

        private void DrawSplitView()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical(GUILayout.Width(PluginListColumnWidth));

                GUILayout.BeginHorizontal();
                DrawSearchBox();
                GUILayout.EndHorizontal();

                _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, false, true);

                try
                {
                    _filteredSetings.Do(DrawPluginInSplitViewList);

                    GUILayout.Space(5);
                    GUILayout.Label(_noOptionsPluginsText.Value + ": " + _modsWithoutSettings, GetLabelStyle());
                    GUILayout.Space(5);
                }
                finally
                {
                    GUILayout.EndScrollView();
                    GUILayout.EndVertical();
                }

                GUILayout.Space(5f);

                PluginSettingsData plugin = _filteredSetings.FirstOrDefault(plg => !plg.Collapsed) ?? _filteredSetings.FirstOrDefault();

                if (plugin != null)
                {
                    plugin.Collapsed = false;

                    GUILayout.BeginVertical(GUILayout.MaxWidth(SettingsListColumnWidth));

                    bool hasCollapsedCategories = plugin.Categories.Any(cat => cat.Collapsed);

                    GUILayout.BeginHorizontal(GetBackgroundStyle());
                    SettingFieldDrawer.DrawPluginHeader(GetPluginHeaderName(plugin, showGUID: true), plugin.Collapsed, hasCollapsedCategories, out bool toggleCollapseAll);
                    GUILayout.EndHorizontal();

                    _settingWindowCategoriesScrollPos = GUILayout.BeginScrollView(_settingWindowCategoriesScrollPos, false, true);
                    try
                    {
                        DrawPluginCategoriesSplitView(plugin, hasCollapsedCategories, toggleCollapseAll);
                    }
                    finally
                    {
                        GUILayout.EndScrollView();
                        GUILayout.EndVertical();
                    }
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSingleColumn()
        {
            _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, false, true);

            var scrollPosition = _settingWindowScrollPos.y;
            var scrollHeight = currentWindowRect.height;

            GUILayout.BeginVertical();
            try
            {
                float currentHeight = 0;

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

                        if (Event.current.type == EventType.Repaint)
                            plugin.Height = (int)GUILayoutUtility.GetLastRect().height;
                    }
                    else
                    {
                        try
                        {
                            if (plugin.Height > 0)
                                GUILayout.Space(plugin.Height);
                        }
                        catch (ArgumentException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                        }
                    }

                    currentHeight += plugin.Height + 1;
                }

                GUILayout.Space(20);
                GUILayout.Label(_noOptionsPluginsText.Value + ": " + _modsWithoutSettings, GetLabelStyle());
                GUILayout.Space(10);
            }
            finally
            {
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
            }
        }

        private void DrawWindowHeader()
        {
            GUI.backgroundColor = _entryBackgroundColor.Value;
            GUILayout.BeginHorizontal();
            {
                GUI.enabled = !IsSearching;

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

                if (GUILayout.Button(_toggleTextEditorText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    configFilesEditor.IsOpen = !configFilesEditor.IsOpen;

                GUILayout.Space(10);

                if (GUILayout.Button(SplitView ? _viewModeSingleColumnText.Value : _viewModeSplitViewText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    SplitView = !SplitView;

                if (GUILayout.Button(_closeText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    DisplayingWindow = false;
            }
            GUILayout.EndHorizontal();

            if (!SplitView)
            {
                GUILayout.BeginHorizontal();
                {
                    DrawSearchBox();

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
        }

        private void DrawSearchBox()
        {
            string label = SplitView ? _searchTextSplitView.Value : _searchText.Value;
            GUILayout.Label(label, GetLabelStyle(), GUILayout.Width(GetLabelStyle().CalcSize(new GUIContent(label)).x + 4));

            GUI.SetNextControlName(SearchBoxName);
            SearchString = GUILayout.TextField(SearchString, GetTextStyle(), GUILayout.ExpandWidth(true));

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
        }

        private void DrawSinglePlugin(PluginSettingsData plugin)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;

            GUILayout.BeginVertical(GetBackgroundStyle(withHover: plugin.Collapsed));

            bool hasCollapsedCategories = plugin.Categories.Any(cat => cat.Collapsed);

            if (SettingFieldDrawer.DrawPluginHeader(GetPluginHeaderName(plugin), plugin.Collapsed, hasCollapsedCategories, out bool toggleCollapseAll) && !IsSearching)
                plugin.Collapsed = !plugin.Collapsed;

            if (IsSearching || !plugin.Collapsed)
            {
                foreach (var category in plugin.Categories)
                    DrawSingleCategory(plugin, hasCollapsedCategories, toggleCollapseAll, category);

                DrawFooterButtons(plugin);
            }

            GUILayout.EndVertical();

            GUI.backgroundColor = backgroundColor;
        }

        private GUIContent GetPluginHeaderName(PluginSettingsData plugin, bool showGUID = false) => new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}{(showGUID ? $" ({plugin.Info.GUID})" : "")}");

        private void DrawPluginInSplitViewList(PluginSettingsData plugin)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;

            GUILayout.BeginHorizontal(GetBackgroundStyle(withHover: true));

            bool hasCollapsedCategories = plugin.Categories.Any(cat => cat.Collapsed);

            if (SettingFieldDrawer.DrawPluginHeaderSplitViewList(GetPluginHeaderName(plugin), !plugin.Collapsed))
            {
                plugin.Collapsed = false;
                _filteredSetings.Where(plg => plg != plugin).Do(plg => plg.Collapsed = true);
            }

            GUILayout.EndHorizontal();

            GUI.backgroundColor = backgroundColor;
        }

        private void DrawPluginCategoriesSplitView(PluginSettingsData plugin, bool hasCollapsedCategories, bool toggleCollapseAll = false)
        {
            foreach (var category in plugin.Categories)
            {
                Color backgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = _entryBackgroundColor.Value;

                DrawSingleCategory(plugin, hasCollapsedCategories, toggleCollapseAll, category);

                GUI.backgroundColor = backgroundColor;
            }

            GUILayout.FlexibleSpace();

            DrawFooterButtons(plugin);
        }

        private void DrawFooterButtons(PluginSettingsData plugin)
        {
            GUILayout.BeginHorizontal();

            var color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            if (!SplitView && GUILayout.Button(_collapseText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                plugin.Collapsed = !plugin.Collapsed;

            GUI.backgroundColor = color;
            GUILayout.EndHorizontal();
        }

        private void DrawSingleCategory(PluginSettingsData plugin, bool hasCollapsedCategories, bool toggleCollapseAll, PluginSettingsData.PluginSettingsGroupData category)
        {
            if (!string.IsNullOrEmpty(category.Name))
            {
                if (!_categoriesCollapseable.Value)
                    category.Collapsed = false;
                else if (toggleCollapseAll && !IsSearching)
                    category.Collapsed = !hasCollapsedCategories;

                if (plugin.Categories.Count > 1 || !_hideSingleSection.Value)
                {
                    GUILayout.BeginVertical(GetCategoryHeaderBackgroundStyle(), GUILayout.ExpandHeight(false));
                    if (category.Collapsed && !IsSearching ? SettingFieldDrawer.DrawCollapsedCategoryHeader(category.Name, category.Settings.All(IsDefaultValue)) : SettingFieldDrawer.DrawCategoryHeader(category.Name) && !IsSearching)
                        category.Collapsed = !category.Collapsed;
                    GUILayout.EndVertical();
                }
            }

            if (category.Settings.Any() && (!category.Collapsed || IsSearching))
            {
                GUILayout.BeginVertical(GetCategoryBackgroundStyle());
                category.Settings.Do(DrawSingleSetting);
                GUILayout.EndVertical();
            }
        }

        private void DrawSingleSetting(SettingEntryBase setting)
        {
            Color contentColor = GUI.contentColor;
            bool enabled = GUI.enabled;

            if (setting.ReadOnly == true && _readOnlyStyle.Value != ReadOnlyStyle.Ignored)
            {
                if (enabled)
                    GUI.enabled = _readOnlyStyle.Value != ReadOnlyStyle.Disabled;

                if (_readOnlyStyle.Value == ReadOnlyStyle.Colored)
                    GUI.contentColor = _readOnlyColor.Value;
            }

            GUILayout.BeginHorizontal();
            
            try
            {
                DrawSettingName(setting);
                _fieldDrawer.DrawSettingValue(setting);
                DrawDefaultButton(setting);
            }
            catch (FormatException)
            {
                LogInfo($"Incorrect input: {setting.PluginInfo.Name} - {setting.Category} - {setting.DispName}");
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, $"Failed to draw setting {setting.PluginInfo.Name} - {setting.Category} - {setting.DispName}:\n{ex}");
                GUILayout.Label("Failed to draw this field, check log for details.", GetLabelStyle());
            }

            GUILayout.EndHorizontal();

            if (enabled && !Utilities.ComboBox.IsShown())
                GUI.enabled = enabled;

            GUI.contentColor = contentColor;
        }

        private void DrawSettingName(SettingEntryBase setting)
        {
            if (setting.HideSettingName) return;
            
            var color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            GUILayout.BeginHorizontal(GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth));
            GUILayout.Label(new GUIContent(setting.DispName.TrimStart('!'), setting.Description), GetLabelStyle());
            GUILayout.FlexibleSpace();
            if (_showTooltipBlock.Value)
                GUILayout.Label(new GUIContent("[?]", setting.Description), GetLabelStyleInfo(), GUILayout.Width(18f));
            GUILayout.EndHorizontal();

            GUI.backgroundColor = color;
        }

        private static void DrawDefaultButton(SettingEntryBase setting)
        {
            if (setting.HideDefaultButton) return;

            Color color = GUI.backgroundColor;
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

            GUI.backgroundColor = color;
        }

        public void BuildSettingList()
        {
            SettingSearcher.CollectSettings(out var results, out var modsWithoutSettings);

            _modsWithoutSettings = string.Join(", ", modsWithoutSettings.Select(x => x.TrimStart('!')).OrderBy(x => x).ToArray());
            _allSettings = results.ToList();

            BuildFilteredSettingList();
        }

        public bool IsSearching => SearchString.Length > 1;

        public void BuildFilteredSettingList()
        {
            IEnumerable<SettingEntryBase> results = _allSettings;

            if (_readOnlyStyle.Value == ReadOnlyStyle.Hidden)
                results = results.Where(x => x.ReadOnly != true);
            if (HideSettings())
                results = results.Where(x => !(x as ConfigSettingEntry).ShouldBeHidden());

            if (IsSearching)
            {
                results = results.Where(x => ContainsSearchString(x, SearchString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)));
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

            var settingsAreCollapsed = _pluginConfigCollapsedDefault.Value;

            var nonDefaultCollapsedPluginState = new HashSet<string>();
            var collapsedCategoryState = new Dictionary<Tuple<string, string>, bool>();
            foreach (var pluginSettings in _filteredSetings)
            {
                if (pluginSettings.Collapsed != settingsAreCollapsed)
                {
                    nonDefaultCollapsedPluginState.Add(pluginSettings.Info.Name);
                }

                foreach (var category in pluginSettings.Categories)
                    collapsedCategoryState[Tuple.Create(pluginSettings.Info.Name, category.Name)] = category.Collapsed;
            }

            _filteredSetings = results
                .GroupBy(x => x.PluginInfo)
                .Select(pluginSettings =>
                {
                    var originalCategoryOrder = pluginSettings.Select(x => x.Category).Distinct().ToList();

                    var categories = pluginSettings
                        .GroupBy(x => x.Category)
                        .OrderBy(x => _sortCategoriesByName.Value ? -1 : originalCategoryOrder.IndexOf(x.Key))
                        .ThenBy(x => x.Key)
                        .Select(x => new PluginSettingsData.PluginSettingsGroupData { 
                            Name = x.Key, 
                            Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList(),
                            Collapsed = _categoriesCollapseable.Value && 
                                            (collapsedCategoryState.TryGetValue(Tuple.Create(pluginSettings.Key.Name, x.Key), out bool collapsed) 
                                            ? collapsed
                                            : _categoriesCollapsedDefault.Value && originalCategoryOrder.Count > 20 && x.All(IsDefaultValue))
                        });
                    
                    return new PluginSettingsData 
                    { 
                        Info = pluginSettings.Key, 
                        Categories = categories.ToList(), 
                        Collapsed = nonDefaultCollapsedPluginState.Contains(pluginSettings.Key.Name) ? !settingsAreCollapsed : settingsAreCollapsed 
                    };
                })
                .OrderBy(x => _orderPluginByGuid.Value ? x.Info.GUID : x.Info.Name)
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

        public const int c_defaultWidth = 700;
        public const int c_defaultHeight = 900;

        private void CalculateDefaultWindowRect()
        {
            var width = Mathf.Min(Screen.width, c_defaultWidth) * (SplitView ? 1f + _splitViewListSize.Value : 1f);
            var height = Mathf.Min(Screen.height, c_defaultHeight);
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 10f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 10f);

            DefaultWindowRect = new Rect(offsetX, offsetY, width, height);

            CalculateSettingsColumnsWidth(DefaultWindowRect.width);
        }

        internal static void DrawTooltip(Rect area)
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                var currentEvent = Event.current;

                var color = GUI.backgroundColor;
                GUI.backgroundColor = _tooltipBackgroundColor.Value;

                string[] lines = GUI.tooltip.Split('\n'); 

                int maxIndex = lines
                    .Select((line, index) => new { Line = line, Index = index })
                    .OrderByDescending(select => select.Line.Length)
                    .First().Index;

                GetTooltipStyle().CalcMinMaxWidth(new GUIContent(lines[maxIndex]), out _, out float width);

                var height = GetTooltipStyle().CalcHeight(new GUIContent(GUI.tooltip), width) + 10;

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

        private void CreateBackgrounds()
        {
            if (WindowBackground == null || EntryBackground == null || TooltipBackground == null)
            {
                var background = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                background.SetPixel(0, 0, _windowBackgroundColor.Value);
                background.Apply();
                WindowBackground = background;

                var entryBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                entryBackground.SetPixel(0, 0, _entryBackgroundColor.Value);
                entryBackground.Apply();
                EntryBackground = entryBackground;

                var tooltipBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                tooltipBackground.SetPixel(0, 0, _tooltipBackgroundColor.Value);
                tooltipBackground.Apply();
                TooltipBackground = tooltipBackground;

                var headerBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                headerBackground.SetPixel(0, 0, _headerBackgroundColor.Value);
                headerBackground.Apply();
                HeaderBackground = headerBackground;
            }
        }

        private void UpdateBackgrounds()
        {
            Destroy(WindowBackground);
            Destroy(EntryBackground);
            Destroy(TooltipBackground);
            Destroy(HeaderBackground);

            WindowBackground = null;
            EntryBackground = null;
            TooltipBackground = null;
            HeaderBackground = null;

            CreateBackgrounds();
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
    }
}
