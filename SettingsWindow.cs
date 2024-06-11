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
        void OnGUI()
        {
            if (DisplayingWindow)
            {
                CreateBackgrounds();
                CreateStyles();
                SetUnlockCursor(0, true);

                GUI.Box(currentWindowRect, GUIContent.none, new GUIStyle());
                GUI.backgroundColor = _windowBackgroundColor.Value;

                currentWindowRect.size = new Vector2(Math.Clamp(_windowSize.Value.x, 400, Screen.width), Math.Clamp(_windowSize.Value.y, 400, Screen.height));

                RightColumnWidth = Mathf.RoundToInt(Mathf.Clamp(currentWindowRect.width / 2.5f * fontSize / 12f, currentWindowRect.width * 0.5f, currentWindowRect.width * 0.8f));
                LeftColumnWidth = Mathf.RoundToInt(Mathf.Clamp(currentWindowRect.width - RightColumnWidth - 100, currentWindowRect.width * 0.2f, currentWindowRect.width * 0.5f)) - 15;

                currentWindowRect = GUILayout.Window(WindowId, currentWindowRect, SettingsWindow, _windowTitle.Value, GetWindowStyle());
                
                if (!UnityInput.Current.GetKey(KeyCode.Mouse0) && (currentWindowRect.x != _windowPosition.Value.x || currentWindowRect.y != _windowPosition.Value.y))
                    SaveCurrentSizeAndPosition();
            }
        }

        internal void SaveCurrentSizeAndPosition()
        {
            _windowPosition.Value = currentWindowRect.position;
            _windowSize.Value = currentWindowRect.size;
            Config.Save();
            SettingFieldDrawer.ClearComboboxCache();
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
                            if (plugin.Height > 0)
                                GUILayout.Space(plugin.Height);
                        }
                        catch (ArgumentException)
                        {
                            // Needed to avoid GUILayout: Mismatched LayoutGroup.Repaint crashes on large lists
                        }
                    }

                    currentHeight += plugin.Height;
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
            SettingSearcher.CollectSettings(out var results, out var modsWithoutSettings, _showDebug);

            _modsWithoutSettings = string.Join(", ", modsWithoutSettings.Select(x => x.TrimStart('!')).OrderBy(x => x).ToArray());
            _allSettings = results.ToList();

            BuildFilteredSettingList();
        }

        public void BuildFilteredSettingList()
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
                if (_readOnlyStyle.Value == ReadOnlyStyle.Hidden)
                    results = results.Where(x => x.ReadOnly != true);
                if (HideSettings())
                    results = results.Where(x => !(x as ConfigSettingEntry).ShouldBeHidden());
            }

            const string shortcutsCatName = "Keyboard shortcuts";

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
                        .GroupBy(eb => eb.Category)
                        .OrderBy(x => string.Equals(x.Key, shortcutsCatName, StringComparison.Ordinal))
                        .ThenBy(x => x.Key)
                        .Select(x => new PluginSettingsData.PluginSettingsGroupData { Name = x.Key, Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList() });
                    return new PluginSettingsData 
                    { 
                        Info = pluginSettings.Key, 
                        Categories = categories.ToList(), 
                        Collapsed = nonDefaultCollpasingStateByPluginName.Contains(pluginSettings.Key.Name) ? !settingsAreCollapsed : settingsAreCollapsed 
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
