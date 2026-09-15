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
        internal const int HeaderSize = 20;
        internal const int DefaultWidth = 750;
        internal const int DefaultHeight = 900;

        internal float scaleFactor;
        internal Matrix4x4 guiMatrix;

        private float lastClickTime;
        private float lastDoubleClickTime;
        private Vector2 lastClickPosition;
        private const float DoubleClickThreshold = 0.3f;

        private ConfigFilesEditor _configFilesEditor;
        private SettingEditWindow _configSettingWindow;
        private readonly List<ConfigSettingEntry> _dynamicAttributeSettings = new List<ConfigSettingEntry>();
        private int _nextAttributeRefresh;
        private const int AttributeRefreshBudget = 64;
        private bool _filterRebuildPending;
        private int _layoutStyleRevision = -1;
        private int _layoutSettingsWidth;
        private int _layoutNameWidth;
        private int _layoutPluginWidth;
        private int _layoutValueWidth;
        private GUILayoutOption[] _rowLayoutOptions;
        private GUILayoutOption[] _nameLayoutOptions;
        private GUILayoutOption[] _pluginLayoutOptions;
        private GUILayoutOption[] _settingsLayoutOptions;
        internal GUILayoutOption[] ValueLayoutOptions { get; private set; }
        private readonly GUIContent _advancedContent = new GUIContent();
        private readonly GUIContent _shortcutsContent = new GUIContent();
        private readonly GUIContent _compactContent = new GUIContent();
        private readonly GUIContent _viewModeContent = new GUIContent();
        private string _viewModeWidthText;
        private int _viewModeStyleRevision = -1;
        private GUILayoutOption[] _viewModeOptions;
        private static readonly GUIContent TooltipContent = new GUIContent();
        private static string _tooltipSource;
        private static GUIStyle _tooltipMeasurementStyle;
        private static Vector2 _tooltipMeasurementBounds;
        private static Vector2 _tooltipSize;
        private bool _windowGeometryDirty;
        private Vector2? _pendingWindowSize;

        internal string _selectedCategory;
        internal string _selectedPlugin;
        internal string _showPluginCategories;

        public bool SplitView
        {
            get => _splitView == null || _splitView.Value;
            set
            {
                if (_splitView != null)
                    _splitView.Value = value;
            }
        }

        void OnGUI()
        {
            if (!DisplayingWindow)
                return;

            if (Event.current.type == EventType.Layout && _filterRebuildPending)
                RebuildFilteredSettingList();
            CreateStyles();
            if (Event.current.type == EventType.Layout && _layoutStyleRevision != Revision)
            {
                _layoutStyleRevision = Revision;
                foreach (PluginSettingsData plugin in _filteredSetings)
                    plugin.Height = 0;
            }
            if (scaleFactor != (scaleFactor = ScaleFactor))
                guiMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleFactor, scaleFactor, 1f));

            if (!_windowGeometryDirty)
            {
                currentWindowRect.size = _windowSize.Value;
                currentWindowRect.position = _windowPosition.Value;
            }

            Matrix4x4 originalMatrix = GUI.matrix;
            Vector2 originalMousePosition = Event.current.mousePosition;
            Color originalBackground = GUI.backgroundColor;
            Color originalColor = GUI.color;
            Color originalContentColor = GUI.contentColor;
            bool originalEnabled = GUI.enabled;
            int originalDepth = GUI.depth;
            try
            {
                GUI.matrix = guiMatrix;
                GUI.depth = -100;
                GUI.tooltip = string.Empty;
                GUI.backgroundColor = _windowBackgroundColor.Value;
                CalculateSettingsColumnsWidth(currentWindowRect.width);
                Rect drawnWindowRect = GUILayout.Window(WindowId, currentWindowRect, SettingsWindow, _windowTitle.Value, GetWindowStyle());
                if (_pendingWindowSize.HasValue)
                {
                    drawnWindowRect.size = _pendingWindowSize.Value;
                    _pendingWindowSize = null;
                }
                currentWindowRect = drawnWindowRect;

                if (currentWindowRect.position != _windowPosition.Value)
                    _windowGeometryDirty = true;

                if (_windowGeometryDirty && !UnityInput.Current.GetMouseButton(0))
                    SaveCurrentSizeAndPosition();

                GUI.backgroundColor = originalBackground;
                GUI.color = originalColor;
                GUI.contentColor = originalContentColor;
                GUI.enabled = originalEnabled;
                _configFilesEditor.OnGUI();
                GUI.backgroundColor = originalBackground;
                GUI.color = originalColor;
                GUI.contentColor = originalContentColor;
                GUI.enabled = originalEnabled;
                _configSettingWindow.OnGUI();
            }
            finally
            {
                Utilities.GUITooltips.EndWindow();
                GUI.matrix = originalMatrix;
                Event.current.mousePosition = originalMousePosition;
                GUI.backgroundColor = originalBackground;
                GUI.color = originalColor;
                GUI.contentColor = originalContentColor;
                GUI.enabled = originalEnabled;
                GUI.depth = originalDepth;
            }
        }

        private void CalculateSettingsColumnsWidth(float width)
        {
            PluginListColumnWidth = Mathf.RoundToInt(width * _splitViewListSize.Value);
            SettingsListColumnWidth = Mathf.RoundToInt(SplitView ? width - PluginListColumnWidth : width);

            LeftColumnWidth = Mathf.Max(200, Mathf.RoundToInt(Mathf.Clamp(SettingsListColumnWidth * _columnSeparatorPosition.Value, width * 0.1f, width * 0.6f)) - fontSize / 2);
            RightColumnWidth = Mathf.Max(200, Mathf.RoundToInt(Mathf.Clamp(SettingsListColumnWidth - LeftColumnWidth - fontSize - 90 - fontSize, width * 0.3f, width * 0.8f)));

            if (_rowLayoutOptions == null || _layoutSettingsWidth != SettingsListColumnWidth)
            {
                _layoutSettingsWidth = SettingsListColumnWidth;
                _rowLayoutOptions = new[] { GUILayout.MaxWidth(SettingsListColumnWidth) };
                _settingsLayoutOptions = _rowLayoutOptions;
                foreach (PluginSettingsData plugin in _filteredSetings)
                    plugin.Height = 0;
            }
            if (_nameLayoutOptions == null || _layoutNameWidth != LeftColumnWidth)
            {
                _layoutNameWidth = LeftColumnWidth;
                _nameLayoutOptions = new[] { GUILayout.Width(LeftColumnWidth), GUILayout.MaxWidth(LeftColumnWidth) };
            }
            if (_pluginLayoutOptions == null || _layoutPluginWidth != PluginListColumnWidth)
            {
                _layoutPluginWidth = PluginListColumnWidth;
                _pluginLayoutOptions = new[] { GUILayout.Width(PluginListColumnWidth) };
            }
            if (ValueLayoutOptions == null || _layoutValueWidth != RightColumnWidth)
            {
                _layoutValueWidth = RightColumnWidth;
                ValueLayoutOptions = new[] { GUILayout.MaxWidth(RightColumnWidth) };
            }
        }

        internal void SaveCurrentSizeAndPosition()
        {
            Vector2 windowSize = new Vector2(
                Mathf.Clamp(currentWindowRect.size.x, 500f, ScreenWidth),
                Mathf.Clamp(currentWindowRect.size.y, 200f, ScreenHeight));
            Vector2 windowPosition = new Vector2(
                Mathf.Clamp(currentWindowRect.position.x, 0f, ScreenWidth - windowSize.x / 4f),
                Mathf.Clamp(currentWindowRect.position.y, 0f, ScreenHeight - HeaderSize * 2));

            SaveOwnConfigChanges(() =>
            {
                _windowSize.Value = windowSize;
                _windowPosition.Value = windowPosition;
            });

            currentWindowRect = new Rect(windowPosition, windowSize);
            _windowGeometryDirty = false;
            _pendingWindowSize = null;
            SettingFieldDrawer.ClearComboboxCache();
        }

        internal void ResetWindowScale()
        {
            _scaleFactor.Value = (float)_scaleFactor.DefaultValue;
        }

        internal void ResetWindowSizeAndPosition()
        {
            Vector2 managerSize = default;
            Vector2 managerPosition = default;

            SaveOwnConfigChanges(() =>
            {
                _splitViewListSize.Value = (float)_splitViewListSize.DefaultValue;
                _columnSeparatorPosition.Value = (float)_columnSeparatorPosition.DefaultValue;

                CalculateDefaultWindowRect();

                managerSize = GetDefaultManagerWindowSize();
                managerPosition = GetDefaultManagerWindowPosition();
                _windowSize.Value = managerSize;
                _windowPosition.Value = managerPosition;
                _windowSizeTextEditor.Value = GetDefaultTextEditorWindowSize();
                _windowPositionTextEditor.Value = GetDefaultTextEditorWindowPosition();
                _windowPositionEditSetting.Value = GetDefaultEditSettingWindowPosition();
                _windowSizeEditSetting.Value = GetDefaultEditSettingWindowSize();
            });

            currentWindowRect = new Rect(managerPosition, managerSize);
            _windowGeometryDirty = false;
            _pendingWindowSize = null;
            SettingFieldDrawer.ClearComboboxCache();
        }

        private void HandleHeaderDblClick(Rect titleBarRect)
        {
            if (Utilities.ComboBox.IsShown())
                return;

            if (UnityInput.Current.GetMouseButtonDown(0) && titleBarRect.Contains(Event.current.mousePosition))
            {
                float time = (float)Math.Round(Time.realtimeSinceStartup, 1);
                if (lastClickPosition == Event.current.mousePosition && time != lastClickTime && time - lastClickTime < DoubleClickThreshold)
                {
                    ResetWindowSizeAndPosition();

                    if (time != lastDoubleClickTime && time - lastDoubleClickTime < DoubleClickThreshold)
                        ResetWindowScale();

                    lastDoubleClickTime = time;
                }

                lastClickTime = time;
                lastClickPosition = Event.current.mousePosition;
            }
        }

        private void SettingsWindow(int id)
        {
            Utilities.GUITooltips.BeginWindow(currentWindowRect);
            Utilities.ComboBox.BeginWindow(id);
            try
            {
                var headerRect = new Rect(0, 0, currentWindowRect.width, HeaderSize);
                HandleHeaderDblClick(headerRect);

                GUI.DragWindow(headerRect);
                DrawWindowHeader();

                var backgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = _entryBackgroundColor.Value;

                if (SplitView)
                    DrawSplitView();
                else
                    DrawListView();

                GUI.backgroundColor = backgroundColor;

                if (!SettingFieldDrawer.DrawCurrentDropdown())
                    DrawTooltip(currentWindowRect);

                Rect resizedWindowRect = Utilities.Utils.ResizeWindow(id, currentWindowRect, out var sizeChanged);
                if (sizeChanged)
                {
                    currentWindowRect = resizedWindowRect;
                    _pendingWindowSize = resizedWindowRect.size;
                    _windowGeometryDirty = true;
                }
            }
            finally
            {
                Utilities.ComboBox.EndWindow();
                Utilities.GUITooltips.EndWindow();
            }
        }

        private void RefreshDynamicSettingAttributes()
        {
            int count = Mathf.Min(AttributeRefreshBudget, _dynamicAttributeSettings.Count);
            for (int index = 0; index < count; ++index)
            {
                if (_nextAttributeRefresh >= _dynamicAttributeSettings.Count)
                    _nextAttributeRefresh = 0;
                if (_dynamicAttributeSettings[_nextAttributeRefresh++].RefreshDisplayAttributes())
                    BuildFilteredSettingList();
            }
        }

        private void DrawSplitView()
        {
            // Resolve and initialize the selected plugin before building either pane. Changing the
            // selection after the left pane has already been laid out makes its control tree differ
            // between Layout and Repaint, which can abort IMGUI with a GUILayoutGroup exception.
            var plugin = _filteredSetings.FirstOrDefault(plg => plg.Selected) ?? _filteredSetings.FirstOrDefault();
            if (plugin != null)
            {
                plugin.Collapsed = false;
                if (!plugin.Selected)
                {
                    plugin.Selected = true;
                    plugin.ShowCategories = true;
                }
            }

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical(_pluginLayoutOptions);

                _settingWindowScrollPos = Utilities.GUITooltips.BeginScrollView(_settingWindowScrollPos, false, true);

                try
                {
                    foreach (PluginSettingsData listedPlugin in _filteredSetings)
                        DrawPluginInSplitViewList(listedPlugin);

                    GUILayout.Space(5);
                    GUILayout.Label(_noOptionsPluginsText.Value + ": " + _modsWithoutSettings, GetLabelStyle());
                    GUILayout.Space(5);
                }
                finally
                {
                    Utilities.GUITooltips.EndScrollView();
                }

                GUILayout.EndVertical();

                GUILayout.Space(5f);

                if (plugin != null)
                {
                    GUILayout.BeginVertical(_settingsLayoutOptions);

                    bool hasCollapsedCategories = plugin.Categories.Any(cat => cat.Collapsed);
                    SettingFieldDrawer.DrawPluginHeader(GetPluginHeaderName(plugin, showGuid: true), plugin.Collapsed, hasCollapsedCategories, withHover:false, out var toggleCollapseAll);

                    _settingWindowCategoriesScrollPos[plugin.Info.GUID] = Utilities.GUITooltips.BeginScrollView(_settingWindowCategoriesScrollPos.TryGetValue(plugin.Info.GUID, out Vector2 scrollPos) ? scrollPos : Vector2.zero, false, true);
                    try
                    {
                        DrawPluginCategories(plugin, hasCollapsedCategories, toggleCollapseAll);
                    }
                    finally
                    {
                        Utilities.GUITooltips.EndScrollView();
                    }

                    GUILayout.EndVertical();
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawListView()
        {
            _settingWindowScrollPos = Utilities.GUITooltips.BeginScrollView(_settingWindowScrollPos, false, true);

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
                Utilities.GUITooltips.EndScrollView();
            }
        }

        private void DrawWindowHeader()
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;
            Utilities.GUIHelper.UpdateContent(_advancedContent, _advancedText.Value, _advancedTextTooltip.Value);
            Utilities.GUIHelper.UpdateContent(_shortcutsContent, _shortcutsText.Value, _shortcutsTextTooltip.Value);
            Utilities.GUIHelper.UpdateContent(_compactContent, _compactListText.Value, _compactListTextTooltip.Value);

            GUILayout.BeginHorizontal();
            {
                var enabled = GUI.enabled;
                GUI.enabled = !IsSearching;

                if (_showAdvanced.Value != (_showAdvanced.Value = Utilities.GUITooltips.Toggle(_showAdvanced.Value, _advancedContent, GetToggleStyle(), Utilities.GUIHelper.FixedWidth)))
                    BuildFilteredSettingList();

                if (_showKeybinds.Value != (_showKeybinds.Value = Utilities.GUITooltips.Toggle(_showKeybinds.Value, _shortcutsContent, GetToggleStyle(), Utilities.GUIHelper.FixedWidth)))
                    BuildFilteredSettingList();

                GUI.enabled = enabled;

                bool compactConfigList = Utilities.GUITooltips.Toggle(
                    _compactConfigList.Value,
                    _compactContent,
                    GetToggleStyle(),
                    Utilities.GUIHelper.FixedWidth);
                if (_compactConfigList.Value != compactConfigList)
                    _compactConfigList.Value = compactConfigList;

                GUILayout.Space(15f);

                DrawSearchBox();

                GUILayout.Space(15f);

                if (GUILayout.Button(_toggleTextEditorText.Value, GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                    _configFilesEditor.IsOpen = !_configFilesEditor.IsOpen;

                GUILayout.Space(15f);

                var maxString = _viewModeListViewText.Value.Length > _viewModeSplitViewText.Value.Length ? _viewModeListViewText.Value : _viewModeSplitViewText.Value;
                if (_viewModeOptions == null || _viewModeWidthText != maxString || _viewModeStyleRevision != Revision)
                {
                    _viewModeWidthText = maxString;
                    _viewModeStyleRevision = Revision;
                    Utilities.GUIHelper.UpdateContent(_viewModeContent, maxString);
                    _viewModeOptions = new[] { Utilities.GUIHelper.FixedWidthOption, GUILayout.Width(GetButtonStyle().CalcSize(_viewModeContent).x) };
                }
                if (GUILayout.Button(SplitView ? _viewModeListViewText.Value : _viewModeSplitViewText.Value, GetButtonStyle(), _viewModeOptions))
                    SplitView = !SplitView;

                if (GUILayout.Button(_closeText.Value, GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                    DisplayingWindow = false;
            }
            GUILayout.EndHorizontal();

            GUI.backgroundColor = backgroundColor;
        }

        private void DrawSearchBox()
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;

            GUI.SetNextControlName(SearchBoxName);
            SearchString = GUILayout.TextField(SearchString, GetTextStyle(), Utilities.GUIHelper.ExpandWidth);

            if (string.IsNullOrEmpty(SearchString) && Event.current.type == EventType.Repaint)
                GUI.Label(GUILayoutUtility.GetLastRect(), _searchText.Value, GetPlaceholderTextStyle());

            if (_focusSearchBox)
            {
                GUI.FocusWindow(WindowId);
                GUI.FocusControl(SearchBoxName);
                _focusSearchBox = false;
            }

            GUI.backgroundColor = _widgetBackgroundColor.Value;

            if (GUILayout.Button(_clearText.Value, GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                SearchString = string.Empty;

            GUI.backgroundColor = backgroundColor;
        }

        private void DrawSinglePlugin(PluginSettingsData plugin)
        {
            GUILayout.BeginVertical();

            try
            {
                var hasCollapsedCategories = plugin.Categories.Any(cat => cat.Collapsed);

                if (SettingFieldDrawer.DrawPluginHeader(GetPluginHeaderName(plugin), plugin.Collapsed, hasCollapsedCategories, withHover:true, out var toggleCollapseAll) && !IsSearching)
                    plugin.Collapsed = !plugin.Collapsed;

                if (IsSearching || !plugin.Collapsed)
                    DrawPluginCategories(plugin, hasCollapsedCategories, toggleCollapseAll);
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        private GUIContent GetPluginHeaderName(PluginSettingsData plugin, bool showGuid = false)
        {
            if (showGuid)
                return plugin.HeaderWithGuidContent ??= new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version} ({plugin.Info.GUID})");
            return plugin.HeaderContent ??= new GUIContent($"{plugin.Info.Name.TrimStart('!')} {plugin.Info.Version}");
        }

        private void DrawPluginInSplitViewList(PluginSettingsData plugin)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;

            GUILayout.BeginVertical(GetPluginSplitViewContainerStyle());
            try
            {
                // Keep the same group tree in both modes to avoid Layout/Repaint mismatches.
                // Compact mode lets the button provide the background; regular mode uses a
                // dedicated tightly-spaced container with the same full-row hover behavior.
                GUILayout.BeginHorizontal(_compactConfigList.Value ? GUIStyle.none : GetPluginHeaderSplitViewBackgroundStyle());
                try
                {
                    if (SettingFieldDrawer.DrawPluginHeaderSplitViewList(GetPluginHeaderName(plugin), plugin.Selected))
                    {
                        plugin.Selected = true;
                        plugin.ShowCategories = !plugin.ShowCategories;
                    }
                }
                finally
                {
                    GUILayout.EndHorizontal();
                }

                if (IsSearching || plugin.Selected && plugin.ShowCategories && (plugin.Categories.Count > 1))
                {
                    GUILayout.BeginVertical(GetCategorySplitViewBackgroundStyle());
                    foreach (PluginSettingsData.PluginSettingsGroupData category in plugin.Categories)
                        DrawPluginCategorySplitViewCollapsableList(plugin, category);
                    GUILayout.EndVertical();
                }
            }
            finally
            {
                GUILayout.EndVertical();
                GUI.backgroundColor = backgroundColor;
            }
        }

        private void DrawPluginCategorySplitViewCollapsableList(PluginSettingsData plugin, PluginSettingsData.PluginSettingsGroupData category)
        {
            GUILayout.BeginHorizontal();
            if (SettingFieldDrawer.DrawPluginCategorySplitViewList(category.Content ??= new GUIContent(category.Name), category.Selected))
            {
                plugin.Selected = true;
                category.Selected = !category.Selected;
                if (category.Selected)
                    category.Collapsed = false;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPluginCategories(PluginSettingsData plugin, bool hasCollapsedCategories, bool toggleCollapseAll = false)
        {
            bool hasSelectedCategory = SplitView && plugin.Categories.Any(cat => cat.Selected);

            foreach (PluginSettingsData.PluginSettingsGroupData category in plugin.Categories)
                DrawSingleCategory(plugin, hasCollapsedCategories, hasSelectedCategory, toggleCollapseAll, category);
        }

        private void DrawSingleCategory(PluginSettingsData plugin, bool hasCollapsedCategories, bool hasSelectedCategory, bool toggleCollapseAll, PluginSettingsData.PluginSettingsGroupData category)
        {
            if (hasSelectedCategory && !category.Selected)
                return;

            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;

            if (!string.IsNullOrEmpty(category.Name))
            {
                if (!_categoriesCollapseable.Value)
                    category.Collapsed = false;
                else if (toggleCollapseAll && !IsSearching)
                    category.Collapsed = !hasCollapsedCategories;

                if (plugin.Categories.Count > 1 || !_hideSingleSection.Value)
                {
                    GUILayout.BeginVertical(GetCategoryHeaderBackgroundStyle(withHover: _categoriesCollapseable.Value), Utilities.GUIHelper.FixedHeight);
                    if (category.Collapsed && !IsSearching ? SettingFieldDrawer.DrawCollapsedCategoryHeader(category.Name, category.Settings.All(IsDefaultValue)) : SettingFieldDrawer.DrawCategoryHeader(category.Name) && !IsSearching)
                        category.Collapsed = !category.Collapsed;
                    GUILayout.EndVertical();
                }
            }

            if (category.Settings.Any() && (!category.Collapsed || IsSearching))
            {
                GUILayout.BeginVertical(GetCategoryBackgroundStyle());
                try
                {
                    foreach (SettingEntryBase setting in category.Settings)
                        DrawSingleSetting(setting);
                }
                finally
                {
                    GUILayout.EndVertical();
                }
            }

            GUI.backgroundColor = backgroundColor;
        }

        private void DrawSingleSetting(SettingEntryBase setting)
        {
            // Drawn entries stay current; the background sweep also finds newly browsable entries.
            // Defer filtering until Layout so this pass keeps the same group/control tree.
            if (setting.RefreshDisplayAttributes())
                BuildFilteredSettingList();

            var contentColor = GUI.contentColor;
            var guiEnabled = GUI.enabled;

            if (setting.ReadOnly == true && _readOnlyStyle.Value != ReadOnlyStyle.Ignored)
            {
                if (guiEnabled)
                    GUI.enabled = _readOnlyStyle.Value != ReadOnlyStyle.Disabled;

                if (_readOnlyStyle.Value == ReadOnlyStyle.Colored)
                    GUI.contentColor = _readOnlyColor.Value;
            }

            GUILayout.BeginHorizontal(GetSettingRowStyle(), _rowLayoutOptions);

            try
            {
                DrawSettingName(setting, guiEnabled);
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

            GUI.enabled = guiEnabled;

            GUI.contentColor = contentColor;
        }

        private void DrawSettingName(SettingEntryBase setting, bool interactionEnabled)
        {
            if (setting.HideSettingName) return;

            var color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            GUILayout.BeginHorizontal(_nameLayoutOptions);
            Utilities.GUITooltips.Label(setting.GetNameContent(), GetLabelStyleSettingName(), Utilities.GUIHelper.ExpandWidth);
            DrawSynchronizationIndicator(setting, interactionEnabled);
            if (_showEditButton.Value)
                //if (setting.CustomDrawer == null && setting.CustomHotkeyDrawer == null || SettingFieldDrawer.IsSettingFailedToCustomDraw(setting))
                    if (Utilities.GUITooltips.Button(setting.GetEditContent(_editText.Value), GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                        _configSettingWindow.EditSetting(setting);

            GUILayout.EndHorizontal();

            GUI.backgroundColor = color;
        }

        private static void DrawSynchronizationIndicator(SettingEntryBase setting, bool interactionEnabled)
        {
            if (!(setting is ConfigSettingEntry configSetting))
                return;

            ConfigSynchronizationState state = configSetting.GetSynchronizationState();
            if (!state.IsVisible)
                return;

            Color symbolColor = state.IsConditional && state.IsOverridden
                ? _changedSynchronizationPolicyColor.Value
                : _fontColor.Value;

            bool enabled = GUI.enabled;
            Color contentColor = GUI.contentColor;
            try
            {
                GUI.enabled = interactionEnabled && state.CanChangePolicy;
                GUI.contentColor = Color.white;
                if (Utilities.GUITooltips.Button(
                        configSetting.GetSynchronizationContent(state, symbolColor),
                        GetSynchronizationIndicatorStyle(),
                        Utilities.GUIHelper.FixedWidth))
                {
                    configSetting.ToggleSynchronizationPolicy();
                }
            }
            finally
            {
                GUI.contentColor = contentColor;
                GUI.enabled = enabled;
            }
        }

        internal static void DrawDefaultButton(SettingEntryBase setting)
        {
            if (setting.HideDefaultButton) return;

            var color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            bool DrawResetButton()
            {
                GUILayout.Space(5);
                return GUILayout.Button(_resetSettingText.Value, GetButtonStyle(), Utilities.GUIHelper.FixedWidth);
            }

            if (setting.DefaultValue != null)
            {
                if (DrawResetButton())
                    setting.Set(setting.DefaultValue);
            }
            else if (setting.SettingType.IsClass)
            {
                if (DrawResetButton())
                    setting.Set(null);
            }

            GUI.backgroundColor = color;
        }

        public void BuildSettingList()
        {
            SettingSearcher.CollectSettings(out var results, out var modsWithoutSettings);

            _modsWithoutSettings = string.Join(", ", modsWithoutSettings.Select(x => x.TrimStart('!')).OrderBy(x => x).ToArray());
            _allSettings = results.ToList();
            SettingFieldDrawer.ClearComboboxCache();
            _dynamicAttributeSettings.Clear();
            foreach (SettingEntryBase setting in _allSettings)
                if (setting is ConfigSettingEntry configSetting && configSetting.HasDynamicAttributes)
                    _dynamicAttributeSettings.Add(configSetting);
            _nextAttributeRefresh = 0;

            BuildFilteredSettingList();
        }

        public bool IsSearching => SearchString.Length > 1;

        public void BuildFilteredSettingList()
        {
            _filterRebuildPending = true;
        }

        private void RebuildFilteredSettingList()
        {
            _filterRebuildPending = false;
            if (_allSettings == null)
                return;

            IEnumerable<SettingEntryBase> results = _allSettings.Where(x => x.Browsable != false);

            if (_readOnlyStyle.Value == ReadOnlyStyle.Hidden)
                results = results.Where(x => x.ReadOnly != true);
            if (HideSettings())
                results = results.Where(x => !(x is ConfigSettingEntry configSetting) || !configSetting.ShouldBeHidden());

            if (IsSearching)
            {
                string[] searchTerms = SearchString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                results = results.Where(x => ContainsSearchString(x, searchTerms));
            }
            else
            {
                if (!_showAdvanced.Value)
                    results = results.Where(x => x.IsAdvanced != true);
                if (_showKeybinds.Value)
                    results = results.Where(x => IsKeyboardShortcut(x));
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
                            ID = $"{pluginSettings.Key.GUID}-{x.Key}",
                            Name = x.Key,
                            Settings = x.OrderByDescending(set => set.Order).ThenBy(set => set.DispName).ToList(),
                            Collapsed = _categoriesCollapseable.Value &&
                                            (collapsedCategoryState.TryGetValue(Tuple.Create(pluginSettings.Key.Name, x.Key), out var collapsed)
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

        private static bool IsKeyboardShortcut(SettingEntryBase x) => x.SettingType == typeof(KeyboardShortcut);

        private static bool ContainsSearchString(SettingEntryBase setting, string[] searchStrings)
        {
            var combinedSearchTarget = setting.PluginInfo.Name + "\n" +
                                       setting.PluginInfo.GUID + "\n" +
                                       setting.DispName + "\n" +
                                       setting.Category + "\n" +
                                       setting.Description + "\n" +
                                       setting.DefaultValue + "\n" +
                                       setting.SettingType.Name + "\n" +
                                       setting.Get();

            return searchStrings.All(s => combinedSearchTarget.IndexOf(s, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private void CalculateDefaultWindowRect()
        {
            var width = Mathf.Min(ScreenSystemWidth, DefaultWidth * (SplitView ? 1f + _splitViewListSize.Value : 1f));
            var height = Mathf.Min(ScreenSystemHeight, DefaultHeight);
            var offset = Mathf.RoundToInt(Mathf.Min(ScreenSystemWidth - width, ScreenSystemHeight - height)) / 16f;

            DefaultWindowRect = new Rect(offset, offset, width, height);

            CalculateSettingsColumnsWidth(DefaultWindowRect.width);
        }

        internal static void DrawTooltip(Rect area)
        {
            string tooltip = Utilities.GUITooltips.GetTooltip();
            if (string.IsNullOrEmpty(tooltip))
                return;

            Rect bounds = Utilities.GUITooltips.GetVisibleWindowRect();
            bounds.xMin += 4f;
            bounds.yMin += 4f;
            bounds.xMax -= 4f;
            bounds.yMax -= 4f;
            if (bounds.width < 20f || bounds.height < 20f)
                return;

            GUIStyle style = GetTooltipStyle();
            if (_tooltipSource != tooltip || !ReferenceEquals(_tooltipMeasurementStyle, style) || _tooltipMeasurementBounds != bounds.size)
            {
                _tooltipSource = tooltip;
                _tooltipMeasurementStyle = style;
                _tooltipMeasurementBounds = bounds.size;
                TooltipContent.text = tooltip.Replace("\r\n", "\n").Replace("\r", "\n");
                style.CalcMinMaxWidth(TooltipContent, out _, out float preferredWidth);
                float preferred = Mathf.Min(Mathf.Max(80f, preferredWidth + 2f), bounds.width * 0.8f);
                float measuredHeight = style.CalcHeight(TooltipContent, preferred);
                if (measuredHeight > bounds.height)
                {
                    preferred = bounds.width;
                    measuredHeight = style.CalcHeight(TooltipContent, preferred);
                }
                _tooltipSize = new Vector2(preferred, Mathf.Min(measuredHeight, bounds.height));
            }
            float width = _tooltipSize.x;
            float height = _tooltipSize.y;

            Vector2 pointer = Event.current.mousePosition;
            float x = Mathf.Clamp(pointer.x + 12f, bounds.xMin, bounds.xMax - width);
            float y = pointer.y + 24f;
            if (y + height > bounds.yMax)
                y = pointer.y - height - 4f;
            y = Mathf.Clamp(y, bounds.yMin, bounds.yMax - height);

            bool enabled = GUI.enabled;
            Color background = GUI.backgroundColor;
            Color color = GUI.color;
            Color contentColor = GUI.contentColor;
            try
            {
                // Read-only fields and synchronization indicators still have useful tooltips.
                GUI.enabled = true;
                GUI.color = Color.white;
                GUI.contentColor = Color.white;
                GUI.backgroundColor = _tooltipBackgroundColor.Value;
                GUI.Box(new Rect(x, y, width, height), TooltipContent, style);
            }
            finally
            {
                GUI.enabled = enabled;
                GUI.backgroundColor = background;
                GUI.color = color;
                GUI.contentColor = contentColor;
            }
        }

        private void CreateBackgrounds()
        {
            if (WindowBackground == null)
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

                var headerBackgroundHover = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                headerBackgroundHover.SetPixel(0, 0, _headerBackgroundHoverColor.Value);
                headerBackgroundHover.Apply();
                HeaderBackgroundHover = headerBackgroundHover;

                var settingWindowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                settingWindowBackground.SetPixel(0, 0, _editWindowBackgroundColor.Value);
                settingWindowBackground.Apply();
                SettingWindowBackground = settingWindowBackground;
            }
        }

        private void UpdateBackgrounds()
        {
            Destroy(WindowBackground);
            Destroy(EntryBackground);
            Destroy(TooltipBackground);
            Destroy(HeaderBackground);
            Destroy(HeaderBackgroundHover);
            Destroy(SettingWindowBackground);

            WindowBackground = null;
            EntryBackground = null;
            TooltipBackground = null;
            HeaderBackground = null;
            HeaderBackgroundHover = null;
            SettingWindowBackground = null;

            CreateBackgrounds();
        }

    }
}
