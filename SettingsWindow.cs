using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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

        void OnGUI()
        {
            if (DisplayingWindow)
            {
                CreateStyles();
                SetUnlockCursor(0, true);

                currentWindowRect.size = _windowSize.Value;
                currentWindowRect.position = _windowPosition.Value;

                GUI.Box(currentWindowRect, GUIContent.none, new GUIStyle());
                GUI.backgroundColor = _windowBackgroundColor.Value;

                RightColumnWidth = Mathf.RoundToInt(Mathf.Clamp(currentWindowRect.width / 2.5f * fontSize / 12f, currentWindowRect.width * 0.5f, currentWindowRect.width * 0.8f));
                LeftColumnWidth = Mathf.RoundToInt(Mathf.Clamp(currentWindowRect.width - RightColumnWidth - 100, currentWindowRect.width * 0.2f, currentWindowRect.width * 0.5f)) - 15;

                currentWindowRect = GUILayout.Window(WindowId, currentWindowRect, SettingsWindow, _windowTitle.Value, GetWindowStyle());

                if (!UnityInput.Current.GetKeyDown(KeyCode.Mouse0) && (currentWindowRect.x != _windowPosition.Value.x || currentWindowRect.y != _windowPosition.Value.y))
                    SaveCurrentSizeAndPosition();
            }
        }

        internal void SaveCurrentSizeAndPosition()
        {
            _windowSize.Value = new Vector2(Mathf.Clamp(currentWindowRect.size.x, 500f, Screen.width), Mathf.Clamp(currentWindowRect.size.y, 200f, Screen.height));
            _windowPosition.Value = new Vector2(Mathf.Clamp(currentWindowRect.position.x, 0f, Screen.width - _windowSize.Value.x / 4f), Mathf.Clamp(currentWindowRect.position.y, 0f, Screen.height - _headerSize));
            Config.Save();
            SettingFieldDrawer.ClearComboboxCache();
        }

        private void SettingsWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, currentWindowRect.width, _headerSize));
            DrawWindowHeader();

            _settingWindowScrollPos = GUILayout.BeginScrollView(_settingWindowScrollPos, false, true);

            var scrollPosition = _settingWindowScrollPos.y;
            var scrollHeight = currentWindowRect.height;

            GUILayout.BeginVertical();
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
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (!SettingFieldDrawer.DrawCurrentDropdown())
                DrawTooltip(currentWindowRect);

            currentWindowRect = Utilities.Utils.ResizeWindow(id, currentWindowRect, out bool sizeChanged);
            
            if (sizeChanged)
                SaveCurrentSizeAndPosition();
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

                newVal = GUILayout.Toggle(_showDebug, "Show mod GUID in tooltip", GetToggleStyle());
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
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;

            GUILayout.BeginVertical(GetBackgroundStyle());

            var categoryHeader = _showDebug ?
                new GUIContent(plugin.Info.Name.TrimStart('!') + " " + plugin.Info.Version, "GUID: " + plugin.Info.GUID) :
                new GUIContent(plugin.Info.Name.TrimStart('!') + " " + plugin.Info.Version);

            bool hasCollapsedCategories = plugin.Categories.Any(cat => cat.Collapsed);

            if (SettingFieldDrawer.DrawPluginHeader(categoryHeader, plugin.Collapsed, hasCollapsedCategories, out bool toggleCollapseAll) && !IsSearching)
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

        private void DrawFooterButtons(PluginSettingsData plugin)
        {
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

            if (GUILayout.Button(_collapseText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
            {
                plugin.Collapsed = !plugin.Collapsed;
            }

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
                    if (category.Collapsed && !IsSearching ? SettingFieldDrawer.DrawCollapsedCategoryHeader(category.Name, category.Settings.All(IsDefaultValue)) : SettingFieldDrawer.DrawCategoryHeader(category.Name) && !IsSearching)
                        category.Collapsed = !category.Collapsed;
            }

            if (!category.Collapsed || IsSearching)
            {
                foreach (var setting in category.Settings)
                {
                    DrawSingleSetting(setting);
                    GUILayout.Space(2);
                }
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

        private void CalculateDefaultWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Mathf.Min(Screen.height, 800);
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
                GUI.backgroundColor = _tooltipBackgroundColor.Value;
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
            }
        }

        private void UpdateBackgrounds()
        {
            Destroy(WindowBackground);
            Destroy(EntryBackground);
            Destroy(TooltipBackground);

            WindowBackground = null;
            EntryBackground = null;
            TooltipBackground = null;

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
