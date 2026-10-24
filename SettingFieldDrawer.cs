// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using BepInEx.Configuration;
using ConfigurationManager.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static ConfigurationManager.ConfigurationManager;
using static ConfigurationManager.ConfigurationManagerStyles;

namespace ConfigurationManager
{
    internal class SettingFieldDrawer
    {
        private static IEnumerable<KeyCode> _keysToCheck;

        public static Dictionary<Type, Action<SettingEntryBase>> SettingDrawHandlers { get; }

        private static readonly Dictionary<SettingEntryBase, ComboBoxCacheEntry> ComboBoxCache = new Dictionary<SettingEntryBase, ComboBoxCacheEntry>();
        private static readonly Dictionary<Type, EnumCacheEntry> EnumCache = new Dictionary<Type, EnumCacheEntry>();
        private static readonly Dictionary<SettingEntryBase, RangeValueLayout> RangeValueLayouts = new Dictionary<SettingEntryBase, RangeValueLayout>();
        private static readonly GUIContent RangeValueSample = new GUIContent();
        private const int MaxRangeValueCharacters = 10;
        private const float MinRangeValueWidth = 50f;

        // Keep incomplete input such as "1" for a 10-60 range until the user finishes editing.
        // Selecting the complete value on focus makes normal typing replace the old number.
        internal sealed class RangeTextEditor
        {
            // Snapshot mouse input before ComboBox can mask or other controls can consume it.
            private static bool windowMouseDown;
            private static int windowEventRevision;

            internal static void BeginWindowEvent()
            {
                Event current = Event.current;
                ++windowEventRevision;
                windowMouseDown = current.type != EventType.Layout &&
                    (current.rawType == EventType.MouseDown || current.type == EventType.MouseDown);
            }

            internal static void EndWindowEvent() => windowMouseDown = false;

            private readonly string controlName;
            private bool editing;
            private bool selectAllPending;
            private bool hasFieldRect;
            private int lastDrawnWindowEvent = -1;
            private Rect fieldRect;
            private string text;
            private string originalText;

            internal RangeTextEditor(string name) => controlName = name;
            internal string DisplayText(string fallback) => editing ? text : fallback;
            internal bool IsEditing => editing;
            internal bool WasDrawnInCurrentWindowEvent => lastDrawnWindowEvent == windowEventRevision;

            private void ReleaseFocusIfOwned()
            {
                // Do not clear the focus of another control which may already have received it.
                if (Event.current != null && GUI.GetNameOfFocusedControl() == controlName)
                    GUI.FocusControl(null);
            }

            internal void Reset(bool releaseFocus = false)
            {
                if (releaseFocus)
                    ReleaseFocusIfOwned();
                editing = false;
                selectAllPending = false;
                hasFieldRect = false;
                text = null;
                originalText = null;
            }

            private bool Finish(out string committed, bool releaseFocus)
            {
                // Focusing a field without modifying its text must not round or rewrite a value.
                bool changed = editing && !string.Equals(text, originalText, StringComparison.Ordinal);
                committed = changed ? text : null;
                Reset(releaseFocus);
                return changed;
            }

            internal bool FinishIfUndrawn(out string committed)
            {
                if (!editing || WasDrawnInCurrentWindowEvent)
                {
                    committed = null;
                    return false;
                }
                return Finish(out committed, releaseFocus: true);
            }

            internal bool FinishOnWindowClose(out string committed) =>
                Finish(out committed, releaseFocus: false);

            internal bool Draw(string actual, GUIStyle style, float width, out string committed)
            {
                lastDrawnWindowEvent = windowEventRevision;

                bool enter = editing && GUI.GetNameOfFocusedControl() == controlName &&
                    Event.current.type == EventType.KeyDown &&
                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

                // The cached rect is captured on Repaint, in the same GUIClip as mousePosition.
                bool clickedOutside = editing && hasFieldRect && windowMouseDown &&
                    !fieldRect.Contains(Event.current.mousePosition);

                GUI.SetNextControlName(controlName);
                string typed = GUILayout.TextField(DisplayText(actual), style, GUILayout.Width(width));
                bool focused = GUI.GetNameOfFocusedControl() == controlName;

                if (Event.current.type == EventType.Repaint)
                {
                    fieldRect = GUILayoutUtility.GetLastRect();
                    hasFieldRect = true;
                }

                if (focused && !editing)
                {
                    editing = true;
                    originalText = actual;
                    selectAllPending = true;
                }

                if (editing)
                    text = typed;

                if (selectAllPending && focused && Event.current.type == EventType.Repaint)
                {
                    TextEditor editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                    editor.SelectAll();
                    selectAllPending = false;
                }

                if (editing && (enter || !focused || clickedOutside))
                {
                    // Preserve the original click; the target control must still receive it.
                    return Finish(out committed, releaseFocus: true);
                }

                committed = null;
                return false;
            }
        }

        private static readonly Dictionary<SettingEntryBase, RangeTextEditor> RangeTextEditors = new Dictionary<SettingEntryBase, RangeTextEditor>();
        // Only track editors actively being used, not every range setting in the window.
        private static readonly HashSet<SettingEntryBase> ActiveRangeTextEditors = new HashSet<SettingEntryBase>();
        private static readonly List<SettingEntryBase> RangeEditorsToFinish = new List<SettingEntryBase>();
        private static int nextRangeTextId;

        private static RangeTextEditor GetRangeTextEditor(SettingEntryBase setting)
        {
            if (!RangeTextEditors.TryGetValue(setting, out RangeTextEditor editor))
                RangeTextEditors.Add(setting, editor = new RangeTextEditor("CM_Range_" + ++nextRangeTextId));
            return editor;
        }

        private static void ApplyPendingRangeText(SettingEntryBase setting, string text)
        {
            if (text == null || setting.ReadOnly == true || setting.AcceptableValueRange.Key == null)
                return;

            try
            {
                float min = (float)Convert.ToDouble(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture);
                float max = (float)Convert.ToDouble(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture);
                if (TryParseRangeText(text, min, max, setting.SettingType, out object parsed) &&
                    !Equals(parsed, setting.Get()))
                    setting.Set(parsed);
            }
            catch (Exception ex)
            {
                LogWarning("Could not apply pending range value for " + setting.SettingID + ": " + ex);
            }
        }

        // An offscreen plugin/category is not drawn by the virtualized list. Finish its
        // pending range edit rather than retaining the buffer until the user scrolls back.
        internal static void FinishUndrawnRangeTextEditors()
        {
            if (ActiveRangeTextEditors.Count == 0)
                return;

            RangeEditorsToFinish.Clear();
            foreach (SettingEntryBase setting in ActiveRangeTextEditors)
            {
                if (!RangeTextEditors.TryGetValue(setting, out RangeTextEditor editor) ||
                    !editor.IsEditing || !editor.WasDrawnInCurrentWindowEvent)
                    RangeEditorsToFinish.Add(setting);
            }

            foreach (SettingEntryBase setting in RangeEditorsToFinish)
            {
                if (RangeTextEditors.TryGetValue(setting, out RangeTextEditor editor) &&
                    editor.FinishIfUndrawn(out string text))
                    ApplyPendingRangeText(setting, text);
                ActiveRangeTextEditors.Remove(setting);
            }
            RangeEditorsToFinish.Clear();
        }

        // Closing the manager must not silently discard a partially typed valid value.
        // The close hotkey may run outside OnGUI, so avoid GUI focus calls here.
        internal static void FinishPendingRangeTextEditors()
        {
            if (ActiveRangeTextEditors.Count == 0)
                return;

            RangeEditorsToFinish.Clear();
            RangeEditorsToFinish.AddRange(ActiveRangeTextEditors);
            foreach (SettingEntryBase setting in RangeEditorsToFinish)
            {
                if (RangeTextEditors.TryGetValue(setting, out RangeTextEditor editor) &&
                    editor.FinishOnWindowClose(out string text))
                    ApplyPendingRangeText(setting, text);
            }
            ActiveRangeTextEditors.Clear();
            RangeEditorsToFinish.Clear();
        }

        internal static bool TryParseRangeText(string text, float minimum, float maximum, Type settingType, out object value)
        {
            value = null;
            if (!float.TryParse(text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ||
                float.IsNaN(number) || float.IsInfinity(number))
                return false;

            try
            {
                value = Convert.ChangeType(Utilities.Utils.RoundWithPrecision(Mathf.Clamp(number, minimum, maximum), _rangePrecision.Value),
                    settingType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (FormatException) { return false; }
            catch (OverflowException) { return false; }
        }

        private sealed class RangeValueLayout
        {
            internal int Revision = -1;
            internal GUIStyle Style;
            internal int Length;
            internal bool Negative;
            internal float Width = MinRangeValueWidth;
        }

        // Reserve enough room for the value and its configured range without letting a single
        // long number squeeze every other slider. Keep the width stable while the value changes.
        internal static float GetRangeValueWidth(SettingEntryBase setting, GUIStyle style, string value, string minimum, string maximum)
        {
            if (!RangeValueLayouts.TryGetValue(setting, out RangeValueLayout layout))
            {
                layout = new RangeValueLayout();
                RangeValueLayouts.Add(setting, layout);
            }

            int length = Math.Min(MaxRangeValueCharacters, Math.Max(value?.Length ?? 0,
                Math.Max(minimum?.Length ?? 0, maximum?.Length ?? 0)));
            bool negative = (value?.StartsWith("-", StringComparison.Ordinal) ?? false)
                || (minimum?.StartsWith("-", StringComparison.Ordinal) ?? false)
                || (maximum?.StartsWith("-", StringComparison.Ordinal) ?? false);

            if (layout.Revision != Revision || !ReferenceEquals(layout.Style, style))
            {
                layout.Revision = Revision;
                layout.Style = style;
                layout.Length = 0;
                layout.Negative = false;
                layout.Width = MinRangeValueWidth;
            }

            if (length > layout.Length || negative && !layout.Negative)
            {
                layout.Length = Math.Max(length, layout.Length);
                layout.Negative |= negative;
                int digits = Math.Max(1, layout.Length - (layout.Negative ? 1 : 0));
                RangeValueSample.text = (layout.Negative ? "-" : "") + new string('0', digits);
                layout.Width = Mathf.Max(layout.Width, Mathf.CeilToInt(style.CalcSize(RangeValueSample).x + style.margin.horizontal));
            }

            return layout.Width;
        }

        private sealed class EnumCacheEntry
        {
            internal readonly IList Values;
            internal readonly bool IsFlags;
            internal readonly FlagValue[] Flags;

            internal EnumCacheEntry(Type type)
            {
                Values = Enum.GetValues(type);
                IsFlags = type.IsDefined(typeof(FlagsAttribute), false);
                if (IsFlags)
                {
                    Flags = new FlagValue[Values.Count];
                    for (int index = 0; index < Values.Count; ++index)
                        Flags[index] = new FlagValue(Values[index]);
                }
            }
        }

        private sealed class FlagValue
        {
            internal readonly GUIContent Content;
            internal readonly long Value;
            private int revision = -1;
            private int width;

            internal FlagValue(object value)
            {
                Content = new GUIContent(value.ToString());
                Value = Convert.ToInt64(value);
            }

            internal int GetWidth(GUIStyle style)
            {
                if (revision != Revision)
                {
                    width = Mathf.CeilToInt(style.CalcSize(Content).x);
                    revision = Revision;
                }
                return width;
            }
        }

        private sealed class ComboBoxCacheEntry
        {
            internal readonly IList Values;
            internal readonly ComboBox Box;
            internal readonly Action<int> OnSelected;
            private readonly SettingEntryBase setting;
            private readonly GUIContent[] contents;
            private object buttonValue;
            private bool hasButtonValue;

            internal ComboBoxCacheEntry(SettingEntryBase setting, IList values, float windowYmax)
            {
                this.setting = setting;
                Values = values;
                contents = new GUIContent[values.Count];
                for (int index = 0; index < values.Count; ++index)
                    contents[index] = ObjectToGuiContent(values[index]);
                Box = new ComboBox(Rect.zero, GUIContent.none, contents, ObjectToGuiContent(setting.DefaultValue),
                    GetButtonStyle(), GetButtonStyle(isDefaultValue: false), GetBoxStyle(), GetComboBoxStyle(), windowYmax);
                OnSelected = Select;
            }

            internal void UpdateButtonContent()
            {
                object value = setting.Get();
                bool immutable = value == null || value is string || value.GetType().IsValueType;
                if (immutable && hasButtonValue && Equals(buttonValue, value))
                    return;
                buttonValue = value;
                hasButtonValue = true;
                for (int index = 0; index < Values.Count; ++index)
                {
                    if (immutable && Equals(Values[index], value))
                    {
                        Box.ButtonContent = contents[index];
                        return;
                    }
                }
                Box.ButtonContent = ObjectToGuiContent(value);
            }

            private void Select(int index)
            {
                if (index >= 0 && index < Values.Count)
                    setting.Set(Values[index]);
            }
        }

        private static EnumCacheEntry GetEnumMetadata(Type type)
        {
            if (!EnumCache.TryGetValue(type, out EnumCacheEntry entry))
            {
                entry = new EnumCacheEntry(type);
                EnumCache.Add(type, entry);
            }
            return entry;
        }
        private static readonly Dictionary<SettingEntryBase, ColorCacheEntry> ColorCache = new Dictionary<SettingEntryBase, ColorCacheEntry>();

        private static ConfigurationManager _instance;

        private static SettingEntryBase _currentKeyboardShortcutToSet;

        public static bool SettingKeyboardShortcut => _currentKeyboardShortcutToSet != null;

        public static readonly HashSet<string> CustomFieldDrawerFailed = new HashSet<string>();

        static SettingFieldDrawer()
        {
            SettingDrawHandlers = new Dictionary<Type, Action<SettingEntryBase>>
            {
                {typeof(bool), DrawBoolField},
                {typeof(KeyboardShortcut), DrawKeyboardShortcut},
                {typeof(KeyCode), DrawKeyCode},
                {typeof(Color), DrawColor },
                {typeof(Vector2), DrawVector2 },
                {typeof(Vector3), DrawVector3 },
                {typeof(Vector4), DrawVector4 },
                {typeof(Quaternion), DrawQuaternion },
            };
        }

        public SettingFieldDrawer(ConfigurationManager instance)
        {
            _instance = instance;
        }

        public void DrawSettingValue(SettingEntryBase setting)
        {
            Color color = GUI.backgroundColor;
            try
            {
                GUI.backgroundColor = _widgetBackgroundColor.Value;
                if (DrawCustomField(setting))
                    return;

                if (setting.AcceptableValueRange.Key != null)
                    DrawRangeField(setting);
                else if (setting.AcceptableValues != null)
                    DrawListField(setting);
                else if (DrawFieldBasedOnValueType(setting))
                    return;
                else if (setting.SettingType.IsEnum)
                    DrawEnumField(setting);
                else
                    DrawUnknownField(setting, _instance.RightColumnWidth);
            }
            finally
            {
                GUI.backgroundColor = color;
            }
        }

        public bool DrawCustomField(SettingEntryBase setting)
        {
            if ((setting.CustomDrawer == null && setting.CustomHotkeyDrawer == null) || IsSettingFailedToCustomDraw(setting))
                return false;

            var color = GUI.contentColor;

            bool result = true;

            var textFieldFontSize = GUI.skin.textField.fontSize;
            var textAreaFontSize = GUI.skin.textArea.fontSize;
            var labelFontSize = GUI.skin.label.fontSize;
            var buttonFontSize = GUI.skin.button.fontSize;

            GUI.skin.textArea.fontSize = fontSize;
            GUI.skin.textField.fontSize = fontSize;
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.button.fontSize = fontSize;

            try
            {
                GUI.contentColor = IsDefaultValue(setting) ? _fontColorValueDefault.Value : _fontColorValueChanged.Value;

                if (setting.CustomDrawer != null)
                    setting.CustomDrawer(setting is ConfigSettingEntry newSetting ? newSetting.Entry : null);
                else if (setting.CustomHotkeyDrawer != null)
                {
                    var isBeingSet = _currentKeyboardShortcutToSet == setting;
                    var isBeingSetOriginal = isBeingSet;
                    setting.CustomHotkeyDrawer(setting is ConfigSettingEntry newSetting ? newSetting.Entry : null, ref isBeingSet);

                    if (isBeingSet != isBeingSetOriginal)
                        _currentKeyboardShortcutToSet = isBeingSet ? setting : null;
                }
                else
                    result = false;
            }
            catch (Exception e)
            {
                SetSettingFailedToCustomDraw(setting, e);
                result = false;
            }

            finally
            {
                GUI.contentColor = color;
                GUI.skin.textField.fontSize = textFieldFontSize;
                GUI.skin.textArea.fontSize = textAreaFontSize;
                GUI.skin.label.fontSize = labelFontSize;
                GUI.skin.button.fontSize = buttonFontSize;
            }

            return result;
        }

        public static bool IsSettingFailedToCustomDraw(SettingEntryBase setting)
        {
            if (setting == null)
                return false;

            return CustomFieldDrawerFailed.Contains(setting.SettingID);
        }

        public static void SetSettingFailedToCustomDraw(SettingEntryBase setting, Exception e = null)
        {
            CustomFieldDrawerFailed.Add(setting.SettingID);

            if (e != null)
                LogWarning(setting.SettingID + "\n" + e);
        }

        public static void ClearCache()
        {
            ClearComboboxCache();
            RangeValueLayouts.Clear();
            RangeTextEditors.Clear();
            ActiveRangeTextEditors.Clear();
            RangeEditorsToFinish.Clear();

            foreach (var tex in ColorCache)
                UnityEngine.Object.Destroy(tex.Value.Tex);
            ColorCache.Clear();
        }

        public static void ClearComboboxCache()
        {
            ComboBoxCache.Clear();
            ComboBox.CloseAll();
        }

        public static void DrawCenteredLabel(string text, GUIStyle labelStyle, float width = 60f)
        {
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(60f, width)));
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        public static bool DrawCategoryHeader(string text)
        {
            if (_categoriesCollapseable.Value)
                return GUILayout.Button(text, GetCategoryStyle(), Utilities.GUIHelper.ExpandWidth);

            GUILayout.Label(text, GetCategoryStyle(), Utilities.GUIHelper.ExpandWidth);
            return false;
        }

        public static bool DrawCollapsedCategoryHeader(string text, bool isDefaultStyle)
        {
            return GUILayout.Button($"> {text} <", GetCategoryStyle(isDefaultStyle), Utilities.GUIHelper.ExpandWidth);
        }

        public static bool DrawPluginHeader(GUIContent content, bool isCollapsed, bool hasCollapsedCategories, bool withHover, out bool toggleCollapseAll)
        {
            GUILayout.BeginHorizontal(GetBackgroundStyle(withHover));

            toggleCollapseAll = false;
            bool state = GUILayout.Button(content, GetHeaderStyle(withHover && !isCollapsed), Utilities.GUIHelper.ExpandWidth);

            if (_categoriesCollapseable.Value)
                toggleCollapseAll = !isCollapsed && GUILayout.Button(hasCollapsedCategories ? "v" : "<", GetButtonStyle(), Utilities.GUIHelper.FixedWidth);

            GUILayout.EndHorizontal();

            return state;
        }

        public static bool DrawPluginHeaderSplitViewList(GUIContent content, bool isActivePlugin) => GUILayout.Button(content, GetHeaderSplitViewStyle(isActivePlugin), Utilities.GUIHelper.ExpandWidth);
        public static bool DrawPluginCategorySplitViewList(GUIContent content, bool isActiveCategory) => GUILayout.Button(content, GetCategorySplitViewStyle(isActiveCategory), Utilities.GUIHelper.ExpandWidth);

        public static bool DrawCurrentDropdown() => ComboBox.DrawCurrentDropdown();

        private static void DrawListField(SettingEntryBase setting)
        {
            var acceptableValues = setting.AcceptableValues;
            if (acceptableValues.Length == 0)
                throw new ArgumentException("AcceptableValueListAttribute returned an empty list of acceptable values. You need to supply at least 1 option.");

            if (!setting.SettingType.IsInstanceOfType(acceptableValues.FirstOrDefault(x => x != null)))
                throw new ArgumentException("AcceptableValueListAttribute returned a list with items of type other than the settng type itself.");

            if (setting.SettingType == typeof(KeyCode))
                DrawKeyCode(setting);
            else
                DrawComboboxField(setting, acceptableValues, _instance.currentWindowRect.yMax);
        }

        private static bool DrawFieldBasedOnValueType(SettingEntryBase setting)
        {
            if (SettingDrawHandlers.TryGetValue(setting.SettingType, out var drawMethod))
            {
                drawMethod(setting);
                return true;
            }
            return false;
        }

        private static void DrawBoolField(SettingEntryBase setting)
        {
            GUI.backgroundColor = _widgetBackgroundColor.Value;
            bool boolVal = (bool)setting.Get();

            Color color = GUI.backgroundColor;
            if (boolVal)
                GUI.backgroundColor = _enabledBackgroundColor.Value;

            bool result = GUILayout.Toggle(boolVal, boolVal ? _enabledText.Value : _disabledText.Value, GetToggleStyle(setting), Utilities.GUIHelper.ExpandWidth);
            if (result != boolVal)
                setting.Set(result);

            if (boolVal)
                GUI.backgroundColor = color;
        }

        private static void DrawEnumField(SettingEntryBase setting)
        {
            EnumCacheEntry metadata = GetEnumMetadata(setting.SettingType);
            if (metadata.IsFlags)
                DrawFlagsField(setting, metadata.Flags, (int)(_instance.RightColumnWidth * 0.8f));
            else
                DrawComboboxField(setting, metadata.Values, _instance.currentWindowRect.yMax);
        }

        private static void DrawFlagsField(SettingEntryBase setting, FlagValue[] allValues, int maxWidth)
        {
            var currentValue = Convert.ToInt64(setting.Get());
            var defaultValue = Convert.ToInt64(setting.DefaultValue);

            // Vertically stack Horizontal groups of the options to deal with the options taking more width than is available in the window
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
            {
                for (var index = 0; index < allValues.Length;)
                {
                    GUILayout.BeginHorizontal();
                    {
                        var currentWidth = 0;
                        bool drewAny = false;
                        for (; index < allValues.Length; index++)
                        {
                            var value = allValues[index];

                            // Skip the 0 / none enum value, just uncheck everything to get 0
                            if (value.Value != 0)
                            {
                                bool curr = (currentValue & value.Value) == value.Value;
                                bool defValue = (defaultValue & value.Value) == value.Value;

                                GUIStyle style = GetToggleStyle(curr == defValue);

                                // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                // GUILayout also needs horizontal margins; measuring only the label can overflow the row.
                                var textDimension = value.GetWidth(style) + style.margin.horizontal;
                                if (currentWidth + textDimension > maxWidth && drewAny)
                                    break;

                                drewAny = true;
                                currentWidth += textDimension;
                                GUI.changed = false;

                                var newVal = GUILayout.Toggle(curr, value.Content, style, Utilities.GUIHelper.FixedWidth);
                                if (GUI.changed)
                                {
                                    var newValue = newVal ? currentValue | value.Value : currentValue & ~value.Value;
                                    setting.Set(Enum.ToObject(setting.SettingType, newValue));
                                }
                            }
                        }

                        // Endless cycle protection
                        if (!drewAny)
                            index++;
                    }
                    GUILayout.EndHorizontal();
                }

                GUI.changed = false;
            }
            GUILayout.EndVertical();
            // Make sure the reset button is properly spaced
            GUILayout.FlexibleSpace();
        }

        private static void DrawComboboxField(SettingEntryBase setting, IList list, float windowYmax)
        {
            if (!ComboBoxCache.TryGetValue(setting, out ComboBoxCacheEntry entry) || !ReferenceEquals(entry.Values, list))
            {
                if (entry != null && entry.Box.IsOpen)
                    ComboBox.CloseAll();
                entry = new ComboBoxCacheEntry(setting, list, windowYmax);
                ComboBoxCache[setting] = entry;
            }

            entry.UpdateButtonContent();
            entry.Box.Rect = GUILayoutUtility.GetRect(entry.Box.ButtonContent, GetButtonStyle(), Utilities.GUIHelper.ExpandWidth);
            entry.Box.Show(entry.OnSelected);
        }

        internal static GUIContent ObjectToGuiContent(object x)
        {
            if (x is Enum)
            {
                var enumType = x.GetType();
                var enumMember = enumType.GetMember(x.ToString()).FirstOrDefault();
                var attr = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault();
                if (attr != null)
                    return new GUIContent(attr.Description);
                return new GUIContent(x.ToString().ToProperCase());
            }
            return new GUIContent(x?.ToString() ?? "NULL");
        }

        private static void DrawRangeField(SettingEntryBase setting)
        {
            var value = setting.Get();
            var converted = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture);

            float height = GetTextStyle(setting).CalcHeight(new GUIContent(value.ToString()), 100f);
            GUILayout.BeginHorizontal(GUILayout.Height(height));

            try
            {
                float result = DrawCenteredHorizontalSlider(converted, leftValue, rightValue, height);

                if (Math.Abs(result - converted) >= Mathf.Abs(rightValue - leftValue) / Math.Pow(10, _rangePrecision.Value + 2))
                {
                    var newValue = Convert.ChangeType(Utilities.Utils.RoundWithPrecision(result, _rangePrecision.Value), setting.SettingType, CultureInfo.InvariantCulture);
                    setting.Set(newValue);
                    GetRangeTextEditor(setting).Reset(releaseFocus: true);
                    ActiveRangeTextEditors.Remove(setting);
                }

                if (setting.ShowRangeAsPercent == true)
                {
                    string percent = $"{Mathf.Abs(result - leftValue) / Mathf.Abs(rightValue - leftValue):P0}";
                    GUIStyle labelStyle = GetLabelStyle(setting);
                    DrawCenteredLabel(percent, labelStyle, GetRangeValueWidth(setting, labelStyle, percent, "100%", null));
                }
                else
                {
                    var strVal = Convert.ToString(value, CultureInfo.InvariantCulture).AppendZeroIfFloat(setting.SettingType);
                    var strMin = Convert.ToString(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture).AppendZeroIfFloat(setting.SettingType);
                    var strMax = Convert.ToString(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture).AppendZeroIfFloat(setting.SettingType);
                    GUIStyle textStyle = GetTextStyle(setting);
                    RangeTextEditor editor = GetRangeTextEditor(setting);
                    bool textChanged = editor.Draw(strVal, textStyle,
                        GetRangeValueWidth(setting, textStyle, editor.DisplayText(strVal), strMin, strMax), out string text);
                    if (editor.IsEditing)
                        ActiveRangeTextEditors.Add(setting);
                    else
                        ActiveRangeTextEditors.Remove(setting);
                    if (textChanged)
                        ApplyPendingRangeText(setting, text);
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private static float DrawCenteredHorizontalSlider(float converted, float leftValue, float rightValue, float height)
        {
            GUILayout.BeginVertical(GUILayout.Height(height));
            GUILayout.Space(height * (_compactConfigList.Value ? 0.3f : 0.45f));
            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GetSliderStyle(), GetThumbStyle(), Utilities.GUIHelper.ExpandWidth);
            GUILayout.EndVertical();
            return result;
        }

        private void DrawUnknownField(SettingEntryBase setting, int rightColumnWidth)
        {
            GUILayout.BeginVertical();
            if (!_compactConfigList.Value)
                GUILayout.Space(4f);

            // Try to use user-supplied converters
            if (setting.ObjToStr != null && setting.StrToObj != null)
            {
                var text = setting.ObjToStr(setting.Get()).AppendZeroIfFloat(setting.SettingType);
                if (text.IsNullOrWhiteSpace() && setting.DefaultValue.ToString() != "")
                    GUI.backgroundColor = _fontColorValueChanged.Value;

                var result = GUILayout.TextArea(text, GetTextStyle(setting), _instance.ValueLayoutOptions).AppendZeroIfFloat(setting.SettingType);
                if (result != text)
                    setting.Set(setting.StrToObj(result));
            }
            else
            {
                // Fall back to slow/less reliable method
                var rawValue = setting.Get();
                var value = rawValue == null ? "NULL" : Convert.ToString(rawValue, CultureInfo.InvariantCulture).AppendZeroIfFloat(setting.SettingType);

                if (value.IsNullOrWhiteSpace() && setting.DefaultValue.ToString() != "")
                    GUI.backgroundColor = _fontColorValueChanged.Value;

                if (CanCovert(value, setting.SettingType))
                {
                    var result = GUILayout.TextField(value, GetTextStyle(setting), _instance.ValueLayoutOptions).AppendZeroIfFloat(setting.SettingType);
                    if (result != value)
                        try
                        {
                            setting.Set(Convert.ChangeType(result, setting.SettingType, CultureInfo.InvariantCulture));
                        }
                        catch
                        {
                            // Don't change anything if format is bad
                        }
                }
                else
                {
                    GUILayout.TextArea(value, GetTextStyle(setting), _instance.ValueLayoutOptions);
                }
            }

            GUILayout.EndVertical();

            // When using MaxWidth the width will always be less than full window size, use this to fill this gap and push the Reset button to the right edge
            GUILayout.FlexibleSpace();
        }

        private readonly Dictionary<Type, bool> _canCovertCache = new Dictionary<Type, bool>();
        private bool CanCovert(string value, Type type)
        {
            if (_canCovertCache.ContainsKey(type))
                return _canCovertCache[type];

            try
            {
                var _ = Convert.ChangeType(value, type);
                _canCovertCache[type] = true;
                return true;
            }
            catch
            {
                _canCovertCache[type] = false;
                return false;
            }
        }

        private static void DrawKeyCode(SettingEntryBase setting)
        {
            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUILayout.Label(_shortcutKeysText.Value, GetLabelStyle(), Utilities.GUIHelper.ExpandWidth);
                GUIUtility.keyboardControl = -1;

                var input = UnityInput.Current;
                if (_keysToCheck == null) _keysToCheck = input.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();
                foreach (var key in _keysToCheck)
                {
                    if (input.GetKeyUp(key))
                    {
                        setting.Set(key);
                        _currentKeyboardShortcutToSet = null;
                        break;
                    }
                }

                if (GUILayout.Button(_cancelText.Value, GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                IList acceptableValues = setting.AcceptableValues?.Length > 1
                    ? setting.AcceptableValues
                    : GetEnumMetadata(setting.SettingType).Values;
                DrawComboboxField(setting, acceptableValues, _instance.currentWindowRect.yMax);

                if (GUILayout.Button(new GUIContent(_shortcutKeyText.Value), GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                    _currentKeyboardShortcutToSet = setting;
            }
        }

        private static void DrawKeyboardShortcut(SettingEntryBase setting)
        {
            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUILayout.Label(_shortcutKeysText.Value, GetButtonStyle(), Utilities.GUIHelper.ExpandWidth);
                GUIUtility.keyboardControl = -1;

                var input = UnityInput.Current;
                if (_keysToCheck == null) _keysToCheck = input.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();
                foreach (var key in _keysToCheck)
                {
                    if (input.GetKeyUp(key))
                    {
                        setting.Set(new KeyboardShortcut(key, _keysToCheck.Where(input.GetKey).ToArray()));
                        _currentKeyboardShortcutToSet = null;
                        break;
                    }
                }

                if (GUILayout.Button(_cancelText.Value, GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                if (GUILayout.Button(FormatKeyboardShortcut((KeyboardShortcut)setting.Get()), GetButtonStyle(setting), Utilities.GUIHelper.ExpandWidth))
                    _currentKeyboardShortcutToSet = setting;

                if (GUILayout.Button(_clearText.Value, GetButtonStyle(), Utilities.GUIHelper.FixedWidth))
                {
                    setting.Set(KeyboardShortcut.Empty);
                    _currentKeyboardShortcutToSet = null;
                }
            }
        }

        private static void DrawVector2(SettingEntryBase obj)
        {
            var setting = (Vector2)obj.Get();
            var copy = setting;
            bool integerValuesOnly = (setting.x % 1 == 0) && (setting.y % 1 == 0);
            setting.x = DrawSingleVectorSlider(setting.x, "X", ((Vector2)obj.DefaultValue).x, integerValuesOnly);
            setting.y = DrawSingleVectorSlider(setting.y, "Y", ((Vector2)obj.DefaultValue).y, integerValuesOnly);
            if (setting != copy) obj.Set(setting);
        }

        private static void DrawVector3(SettingEntryBase obj)
        {
            var setting = (Vector3)obj.Get();
            var copy = setting;
            bool integerValuesOnly = (setting.x % 1 == 0) && (setting.y % 1 == 0) && (setting.z % 1 == 0);
            setting.x = DrawSingleVectorSlider(setting.x, "X", ((Vector3)obj.DefaultValue).x, integerValuesOnly);
            setting.y = DrawSingleVectorSlider(setting.y, "Y", ((Vector3)obj.DefaultValue).y, integerValuesOnly);
            setting.z = DrawSingleVectorSlider(setting.z, "Z", ((Vector3)obj.DefaultValue).z, integerValuesOnly);
            if (setting != copy) obj.Set(setting);
        }

        private static void DrawVector4(SettingEntryBase obj)
        {
            var setting = (Vector4)obj.Get();
            var copy = setting;
            bool integerValuesOnly = (setting.x % 1 == 0) && (setting.y % 1 == 0) && (setting.z % 1 == 0) && (setting.w % 1 == 0);
            setting.x = DrawSingleVectorSlider(setting.x, "X", ((Vector4)obj.DefaultValue).x, integerValuesOnly);
            setting.y = DrawSingleVectorSlider(setting.y, "Y", ((Vector4)obj.DefaultValue).y, integerValuesOnly);
            setting.z = DrawSingleVectorSlider(setting.z, "Z", ((Vector4)obj.DefaultValue).z, integerValuesOnly);
            setting.w = DrawSingleVectorSlider(setting.w, "W", ((Vector4)obj.DefaultValue).w, integerValuesOnly);
            if (setting != copy) obj.Set(setting);
        }

        private static void DrawQuaternion(SettingEntryBase obj)
        {
            var setting = (Quaternion)obj.Get();
            var copy = setting;
            bool integerValuesOnly = (setting.x % 1 == 0) && (setting.y % 1 == 0) && (setting.z % 1 == 0) && (setting.w % 1 == 0);
            setting.x = DrawSingleVectorSlider(setting.x, "X", ((Quaternion)obj.DefaultValue).x, integerValuesOnly);
            setting.y = DrawSingleVectorSlider(setting.y, "Y", ((Quaternion)obj.DefaultValue).y, integerValuesOnly);
            setting.z = DrawSingleVectorSlider(setting.z, "Z", ((Quaternion)obj.DefaultValue).z, integerValuesOnly);
            setting.w = DrawSingleVectorSlider(setting.w, "W", ((Quaternion)obj.DefaultValue).w, integerValuesOnly);
            if (setting != copy) obj.Set(setting);
        }

        private static float DrawSingleVectorSlider(float setting, string label, float defaultValue, bool integerValuesOnly)
        {
            GUILayout.Label(label, GetLabelStyle(), Utilities.GUIHelper.FixedWidth);
            int precision = _vectorDynamicPrecision.Value && integerValuesOnly ? 0 : Math.Abs(_vectorPrecision.Value);
            string value = GUILayout.TextField(setting.ToString("F" + precision, CultureInfo.InvariantCulture), GetTextStyle(setting, defaultValue), Utilities.GUIHelper.ExpandWidth);
            if (precision == 0 && (value.EndsWith(".") || value.EndsWith(",")))
                value = string.Concat(value, string.Empty.PadRight(Math.Abs(_vectorPrecision.Value - 1), '0'), 1);

            Utilities.Utils.TryParseFloat(value, out var x);
            return x;
        }

        private static void DrawColor(SettingEntryBase obj)
        {
            Color currentSetting = (Color)obj.Get();
            Color setting = Utilities.Utils.RoundColorToHEX(currentSetting);
            Color defaultColor = Utilities.Utils.RoundColorToHEX((Color)obj.DefaultValue);

            GUILayout.BeginVertical();
            GUILayout.BeginVertical(GetBoxStyle());
            GUILayout.BeginHorizontal();
            bool isDefaultValue = DrawHexField(ref setting, defaultColor);

            GUILayout.Space(3f);
            GUIHelper.BeginColor(setting);
            // Use the same layout metrics as the adjacent HEX field so the preview stays
            // vertically aligned when compact mode changes text field margins.
            GUILayout.Label(string.Empty, GetTextStyle(isDefaultValue), Utilities.GUIHelper.ExpandWidth);

            if (!ColorCache.TryGetValue(obj, out var cacheEntry))
            {
                cacheEntry = new ColorCacheEntry { Tex = new Texture2D(40, 10, TextureFormat.ARGB32, false), Last = setting };
                cacheEntry.Tex.FillTexture(setting);
                ColorCache[obj] = cacheEntry;
            }

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(GUILayoutUtility.GetLastRect(), cacheEntry.Tex);
            }

            GUIHelper.EndColor();
            GUILayout.Space(3f);

            GUILayout.EndHorizontal();

            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();

            DrawColorField("R", ref setting, ref setting.r, Utilities.Utils.RoundColor(setting.r) == Utilities.Utils.RoundColor(defaultColor.r));
            GUILayout.Space(3f);
            DrawColorField("G", ref setting, ref setting.g, Utilities.Utils.RoundColor(setting.g) == Utilities.Utils.RoundColor(defaultColor.g));
            GUILayout.Space(3f);
            DrawColorField("B", ref setting, ref setting.b, Utilities.Utils.RoundColor(setting.b) == Utilities.Utils.RoundColor(defaultColor.b));
            GUILayout.Space(3f);
            DrawColorField("A", ref setting, ref setting.a, Utilities.Utils.RoundColor(setting.a) == Utilities.Utils.RoundColor(defaultColor.a));

            Color normalizedSetting = Utilities.Utils.RoundColorToHEX(setting);

            // Opening the UI can slightly change float channels while keeping the same HEX value.
            // Only write back when the effective config color actually changed.
            if (!IsEqualColorConfig(normalizedSetting, currentSetting))
            {
                obj.Set(normalizedSetting);
            }

            if (!IsEqualColorConfig(normalizedSetting, cacheEntry.Last))
            {
                cacheEntry.Tex.FillTexture(normalizedSetting);
                cacheEntry.Last = normalizedSetting;
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(2f);
            GUILayout.EndVertical();
        }

        private static bool DrawHexField(ref Color value, Color defaultValue)
        {
            GUIStyle style = GetTextStyle(value, defaultValue);
            string currentText = $"#{ColorUtility.ToHtmlStringRGBA(value)}";
            Utilities.Utils.UpdateHexString(ref currentText, GUILayout.TextField(currentText, style, GUILayout.Width(style.CalcSize(new GUIContent("#CCCCCCCC.")).x), Utilities.GUIHelper.FixedWidthOption));
            if (ColorUtility.TryParseHtmlString(currentText, out Color color))
                value = color;

            return IsEqualColorConfig(value, defaultValue);
        }

        private static void DrawColorField(string fieldLabel, ref Color settingColor, ref float settingValue, bool isDefaultValue)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(fieldLabel, GetLabelStyle(), Utilities.GUIHelper.ExpandWidth);

            string currentText = Utilities.Utils.RoundWithPrecision(settingValue, 3).ToString("0.000");
            string valueString = GUILayout.TextField(currentText, GetTextStyle(isDefaultValue), GUILayout.MaxWidth(45f), Utilities.GUIHelper.ExpandWidthOption);
            if (valueString.StartsWith('1'))
                SetColorValue(ref settingColor, 1f);
            else if (valueString.StartsWith('0') && settingValue == 1f)
                SetColorValue(ref settingColor, 0f);
            else if (Utilities.Utils.TryParseFloat(valueString, out float value))
                SetColorValue(ref settingColor, value);

            GUILayout.EndHorizontal();
            GUILayout.Space(1f);

            SetColorValue(ref settingColor, GUILayout.HorizontalSlider(settingValue, 0f, 1f, Utilities.GUIHelper.ExpandWidth));

            GUILayout.EndVertical();

            void SetColorValue(ref Color color, float value)
            {
                float roundedValue = Utilities.Utils.RoundWithPrecision(value, 3);
                switch (fieldLabel)
                {
                    case "R": color.r = roundedValue; break;
                    case "G": color.g = roundedValue; break;
                    case "B": color.b = roundedValue; break;
                    case "A": color.a = roundedValue; break;
                }
            }
        }

        private sealed class ColorCacheEntry
        {
            public Color Last;
            public Texture2D Tex;
        }
    }
}
