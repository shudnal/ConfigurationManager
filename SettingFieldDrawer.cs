﻿// Based on code made by MarC0 / ManlyMarco
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

        private static readonly Dictionary<SettingEntryBase, ComboBox> _comboBoxCache = new Dictionary<SettingEntryBase, ComboBox>();
        private static readonly Dictionary<SettingEntryBase, ColorCacheEntry> _colorCache = new Dictionary<SettingEntryBase, ColorCacheEntry>();

        private static ConfigurationManager _instance;

        private static SettingEntryBase _currentKeyboardShortcutToSet;
        
        public static bool SettingKeyboardShortcut => _currentKeyboardShortcutToSet != null;

        public static readonly HashSet<string> customFieldDrawerFailed = new HashSet<string>();
        
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
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            if (DrawCustomField(setting))
                return;

            if (setting.ShowRangeAsPercent != null && setting.AcceptableValueRange.Key != null)
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

        public bool DrawCustomField(SettingEntryBase setting)
        {
            if (IsSettingFailedToCustomDraw(setting))
                return false;

            var color = GUI.contentColor;

            bool result = true;
            try
            {
                GUI.contentColor = setting.Get().ToString().Equals(setting.DefaultValue.ToString(), StringComparison.OrdinalIgnoreCase) ? _fontColorValueDefault.Value : _fontColorValueChanged.Value;

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

            GUI.contentColor = color;
            return result;
        }

        public static bool IsSettingFailedToCustomDraw(SettingEntryBase setting)
        {
            if (setting == null)
                return false;

            return customFieldDrawerFailed.Contains(GetSettingID(setting));
        }

        public static void SetSettingFailedToCustomDraw(SettingEntryBase setting, Exception e = null)
        {
            string settingID = GetSettingID(setting);
            customFieldDrawerFailed.Add(settingID);

            if (e != null)
                LogWarning(settingID + "\n" + e);
        }

        public static string GetSettingID(SettingEntryBase setting)
        {
            return $"{setting.PluginInfo.GUID}-{setting.Category}-{setting.DispName}";
        }

        public static void ClearCache()
        {
            ClearComboboxCache();

            foreach (var tex in _colorCache)
                UnityEngine.Object.Destroy(tex.Value.Tex);
            _colorCache.Clear();
        }

        public static void ClearComboboxCache()
        {
            _comboBoxCache.Clear();
        }

        public static void DrawCenteredLabel(string text, GUIStyle labelStyle, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public static bool DrawCategoryHeader(string text)
        {
            if (_categoriesCollapseable.Value)
                return GUILayout.Button(text, GetCategoryStyle(), GUILayout.ExpandWidth(true));

            GUILayout.Label(text, GetCategoryStyle(), GUILayout.ExpandWidth(true));
            return false;
        }

        public static bool DrawCollapsedCategoryHeader(string text, bool isDefaultStyle)
        {
            return GUILayout.Button($"> {text} <", GetCategoryStyle(isDefaultStyle), GUILayout.ExpandWidth(true));
        }

        public static bool DrawPluginHeader(GUIContent content, bool isCollapsed, bool hasCollapsedCategories, out bool toggleCollapseAll)
        {
            toggleCollapseAll = false;
            if (!_categoriesCollapseable.Value)
                return DrawPluginHeaderLabel(content);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool state = DrawPluginHeaderLabel(content);
            GUILayout.FlexibleSpace();
            toggleCollapseAll = !isCollapsed && GUILayout.Button(hasCollapsedCategories ? "v" : "<", GetButtonStyle(), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            return state;
        }

        private static bool DrawPluginHeaderLabel(GUIContent content) => GUILayout.Button(content, GetHeaderStyle(), GUILayout.ExpandWidth(true));

        public static bool DrawCurrentDropdown()
        {
            if (ComboBox.CurrentDropdownDrawer != null)
            {
                ComboBox.CurrentDropdownDrawer.Invoke();
                ComboBox.CurrentDropdownDrawer = null;
                return true;
            }
            return false;
        }

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

            bool result = GUILayout.Toggle(boolVal, boolVal ? _enabledText.Value : _disabledText.Value, GetToggleStyle(setting), GUILayout.ExpandWidth(true));
            if (result != boolVal)
                setting.Set(result);

            if (boolVal)
                GUI.backgroundColor = color;
        }

        private static void DrawEnumField(SettingEntryBase setting)
        {
            if (setting.SettingType.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                DrawFlagsField(setting, Enum.GetValues(setting.SettingType), (int)(_instance.RightColumnWidth * 0.8f));
            else
                DrawComboboxField(setting, Enum.GetValues(setting.SettingType), _instance.currentWindowRect.yMax);
        }

        private static void DrawFlagsField(SettingEntryBase setting, IList enumValues, int maxWidth)
        {
            var currentValue = Convert.ToInt64(setting.Get());
            var defaultValue = Convert.ToInt64(setting.DefaultValue);

            var allValues = enumValues.Cast<Enum>().Select(x => new { name = x.ToString(), val = Convert.ToInt64(x) }).ToArray();

            // Vertically stack Horizontal groups of the options to deal with the options taking more width than is available in the window
            GUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));
            {
                for (var index = 0; index < allValues.Length;)
                {
                    GUILayout.BeginHorizontal();
                    {
                        var currentWidth = 0;
                        for (; index < allValues.Length; index++)
                        {
                            var value = allValues[index];

                            // Skip the 0 / none enum value, just uncheck everything to get 0
                            if (value.val != 0)
                            {
                                // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                var textDimension = (int)GUI.skin.toggle.CalcSize(new GUIContent(value.name)).x;
                                currentWidth += textDimension;
                                if (currentWidth > maxWidth)
                                    break;

                                GUI.changed = false;

                                bool curr = (currentValue & value.val) == value.val;
                                bool defValue = (defaultValue & value.val) == value.val;
                                var newVal = GUILayout.Toggle(curr, value.name, GetToggleStyle(curr == defValue), GUILayout.ExpandWidth(false));
                                if (GUI.changed)
                                {
                                    var newValue = newVal ? currentValue | value.val : currentValue & ~value.val;
                                    setting.Set(Enum.ToObject(setting.SettingType, newValue));
                                }
                            }
                        }
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
            var buttonText = ObjectToGuiContent(setting.Get());
            var dispRect = GUILayoutUtility.GetRect(buttonText, GetButtonStyle(), GUILayout.ExpandWidth(true));

            if (!_comboBoxCache.TryGetValue(setting, out var box))
            {
                box = new ComboBox(dispRect, buttonText, 
                                list.Cast<object>().Select(ObjectToGuiContent).ToArray(), ObjectToGuiContent(setting.DefaultValue),
                                GetButtonStyle(), GetButtonStyle(isDefaulValue:false),
                                GetBoxStyle(),
                                GetComboBoxStyle(), 
                                windowYmax);

                _comboBoxCache[setting] = box;
            }
            else
            {
                box.Rect = dispRect;
                box.ButtonContent = buttonText;
            }

            box.Show(id =>
            {
                if (id >= 0 && id < list.Count)
                    setting.Set(list[id]);
            });
        }

        private static GUIContent ObjectToGuiContent(object x)
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
            return new GUIContent(x.ToString());
        }

        private static void DrawRangeField(SettingEntryBase setting)
        {
            var value = setting.Get();
            var converted = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture);

            float result = DrawCenteredHorizontalSlider(converted, leftValue, rightValue);

            if (Math.Abs(result - converted) >= Mathf.Abs(rightValue - leftValue) / Math.Pow(10, _rangePrecision.Value + 2))
            {
                var newValue = Convert.ChangeType(Utilities.Utils.RoundWithPrecision(result, _rangePrecision.Value), setting.SettingType, CultureInfo.InvariantCulture);
                setting.Set(newValue);
            }

            if (setting.ShowRangeAsPercent == true)
            {
                DrawCenteredLabel(
                    $"{Mathf.Abs(result - leftValue) / Mathf.Abs(rightValue - leftValue):P0}",
                    GetLabelStyle(setting),
                    GUILayout.Width(60));
            }
            else
            {
                var strVal = value.ToString().Replace(',', '.').AppendZeroIfFloat(setting.SettingType);
                var strResult = GUILayout.TextField(strVal, GetTextStyle(setting), GUILayout.Width(50));
                if (strResult != strVal && Utilities.Utils.TryParseFloat(strResult, out float resultVal))
                {
                    var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                    setting.Set(Convert.ChangeType(Utilities.Utils.RoundWithPrecision(clampedResultVal, _rangePrecision.Value), setting.SettingType));
                }
            }
        }

        private static float DrawCenteredHorizontalSlider(float converted, float leftValue, float rightValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Space(4);
            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GetSliderStyle(), GetThumbStyle(), GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            return result;
        }

        private void DrawUnknownField(SettingEntryBase setting, int rightColumnWidth)
        {
            // Try to use user-supplied converters
            if (setting.ObjToStr != null && setting.StrToObj != null)
            {
                var text = setting.ObjToStr(setting.Get()).AppendZeroIfFloat(setting.SettingType);
                if (text.IsNullOrWhiteSpace() && setting.DefaultValue.ToString() != "")
                    GUI.backgroundColor = _fontColorValueChanged.Value;

                var result = GUILayout.TextField(text, GetTextStyle(setting), GUILayout.MaxWidth(rightColumnWidth));
                if (result != text)
                    setting.Set(setting.StrToObj(result));
            }
            else
            {
                // Fall back to slow/less reliable method
                var rawValue = setting.Get();
                var value = rawValue == null ? "NULL" : rawValue.ToString().AppendZeroIfFloat(setting.SettingType);

                if (value.IsNullOrWhiteSpace() && setting.DefaultValue.ToString() != "")
                    GUI.backgroundColor = _fontColorValueChanged.Value;

                if (CanCovert(value, setting.SettingType))
                {
                    var result = GUILayout.TextField(value, GetTextStyle(setting), GUILayout.MaxWidth(rightColumnWidth));
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
                    GUILayout.TextArea(value, GetTextStyle(setting), GUILayout.MaxWidth(rightColumnWidth));
                }
            }

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
                GUILayout.Label(_shortcutKeysText.Value, GetLabelStyle(), GUILayout.ExpandWidth(true));
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

                if (GUILayout.Button(_cancelText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                var acceptableValues = setting.AcceptableValues?.Length > 1 ? setting.AcceptableValues : Enum.GetValues(setting.SettingType);
                DrawComboboxField(setting, acceptableValues, _instance.currentWindowRect.yMax);

                if (GUILayout.Button(new GUIContent(_shortcutKeyText.Value), GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = setting;
            }
        }

        private static void DrawKeyboardShortcut(SettingEntryBase setting)
        {
            var value = setting.Get();
            if (value.GetType() == typeof(KeyCode)){
                value = new KeyboardShortcut((KeyCode)value);
            }

            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUILayout.Label(_shortcutKeysText.Value, GetButtonStyle(), GUILayout.ExpandWidth(true));
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

                if (GUILayout.Button(_cancelText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                if (GUILayout.Button(setting.Get().ToString(), GetButtonStyle(setting), GUILayout.ExpandWidth(true)))
                    _currentKeyboardShortcutToSet = setting;

                if (GUILayout.Button(_clearText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
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
            GUILayout.Label(label, GetLabelStyle(), GUILayout.ExpandWidth(false));
            int precision = _vectorDynamicPrecision.Value && integerValuesOnly ? 0 : Math.Abs(_vectorPrecision.Value);
            string value = GUILayout.TextField(setting.ToString("F" + precision, CultureInfo.InvariantCulture), GetTextStyle(setting, defaultValue), GUILayout.ExpandWidth(true)).Replace(',', '.');
            if (precision == 0 && value.EndsWith('.'))
                value = string.Concat(value, string.Empty.PadRight(Math.Abs(_vectorPrecision.Value - 1), '0'), 1);

            float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            return x;
        }

        private static void DrawColor(SettingEntryBase obj)
        {
            Color setting = (Color)obj.Get();

            GUILayout.BeginVertical(GetBoxStyle());
            GUILayout.BeginHorizontal();
            bool isDefaultValue = DrawHexField(ref setting, (Color)obj.DefaultValue);

            GUILayout.Space(3f);
            GUIHelper.BeginColor(setting);
            GUILayout.Label(string.Empty, GUILayout.ExpandWidth(true));

            if (!_colorCache.TryGetValue(obj, out var cacheEntry))
            {
                cacheEntry = new ColorCacheEntry { Tex = new Texture2D(40, 10, TextureFormat.ARGB32, false), Last = setting };
                cacheEntry.Tex.FillTexture(setting);
                _colorCache[obj] = cacheEntry;
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

            DrawColorField("R", ref setting, ref setting.r, isDefaultValue);
            GUILayout.Space(3f);
            DrawColorField("G", ref setting, ref setting.g, isDefaultValue);
            GUILayout.Space(3f);
            DrawColorField("B", ref setting, ref setting.b, isDefaultValue);
            GUILayout.Space(3f);
            DrawColorField("A", ref setting, ref setting.a, isDefaultValue);

            if (setting != cacheEntry.Last)
            {
                obj.Set(setting);
                cacheEntry.Tex.FillTexture(setting);
                cacheEntry.Last = setting;
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private static bool DrawHexField(ref Color value, Color defaultValue)
        {
            string currentText = $"#{ColorUtility.ToHtmlStringRGBA(value)}";
            string textValue = GUILayout.TextField(currentText, GetTextStyle(value, defaultValue), GUILayout.MaxWidth(Mathf.Clamp(95f * fontSize / 14, 80f, 180f)), GUILayout.ExpandWidth(false));
            if (textValue != currentText && ColorUtility.TryParseHtmlString(textValue, out Color color))
                value = color;

            return IsEqualColorConfig(value, defaultValue);
        }

        private static void DrawColorField(string fieldLabel, ref Color settingColor, ref float settingValue, bool isDefaultValue)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(fieldLabel, GetLabelStyle(), GUILayout.ExpandWidth(true));

            string currentText = settingValue.ToString("0.000");
            SetColorValue(ref settingColor, float.Parse(GUILayout.TextField(currentText, GetTextStyle(isDefaultValue), GUILayout.MaxWidth(45f), GUILayout.ExpandWidth(true))));

            GUILayout.EndHorizontal();
            GUILayout.Space(1f);

            SetColorValue(ref settingColor, GUILayout.HorizontalSlider(settingValue, 0f, 1f, GUILayout.ExpandWidth(true)));

            GUILayout.EndVertical();

            void SetColorValue(ref Color color, float value)
            {
                switch (fieldLabel)
                {
                    case "R":
                        color.r = RoundTo000();
                        break;
                    case "G":
                        color.g = RoundTo000();
                        break;
                    case "B":
                        color.b = RoundTo000();
                        break;
                    case "A":
                        color.a = RoundTo000();
                        break;
                }

                float RoundTo000()
                {
                    return Utilities.Utils.RoundWithPrecision(value, 3);
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