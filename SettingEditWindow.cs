using BepInEx;
using BepInEx.Configuration;
using ConfigurationManager.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using static ConfigurationManager.ConfigurationManager;
using static ConfigurationManager.ConfigurationManagerStyles;

namespace ConfigurationManager
{
    internal class SettingEditWindow
    {
        private Rect _windowRect = new Rect(_windowPositionEditSetting.Value, _windowSizeEditSetting.Value);

        private const int WindowId = -6800;

        private SettingEntryBase setting;

        private static SettingEntryBase _currentKeyboardShortcutToSet;
        public Dictionary<Type, Action> SettingDrawHandlers { get; }
        
        private static IEnumerable<KeyCode> _keysToCheck;

        private static readonly Dictionary<SettingEntryBase, ColorCacheEntry> ColorCache = new Dictionary<SettingEntryBase, ColorCacheEntry>();

        private Vector2 _scrollPosition = Vector2.zero;
        private Vector2 _enumScrollPosition = Vector2.zero;

        private int listIndex = -1;
        private IList listEnum = null;

        private Action drawerFunction;

        private StringBuilder errorText = new StringBuilder(10);

        private object valueToSet;

        private string errorOnSetting;

        public SettingEditWindow()
        {
            SettingDrawHandlers = new Dictionary<Type, Action>
            {
                { typeof(bool), DrawBoolField},
                { typeof(KeyboardShortcut), DrawKeyboardShortcut},
                { typeof(KeyCode), DrawKeyCode},
                { typeof(Color), DrawColor },
                { typeof(Vector2), DrawVector2 },
                { typeof(Vector3), DrawVector3 },
                { typeof(Vector4), DrawVector4 },
                { typeof(Quaternion), DrawQuaternion },
            };
        }

        public void EditSetting(SettingEntryBase setting)
        {
            if (this.setting == setting && IsOpen)
            {
                IsOpen = false;
                return;
            }

            this.setting = setting;
            
            InitializeWindow();

            IsOpen = true;
        }

        public bool IsOpen { get; set; }

        public void OnGUI()
        {
            if (!IsOpen)
                return;

            _windowRect.size = _windowSizeEditSetting.Value;
            _windowRect.position = _windowPositionEditSetting.Value;

            Color color = GUI.backgroundColor;
            GUI.backgroundColor = _windowBackgroundColor.Value;

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow, $"{setting.PluginInfo.Name} {setting.PluginInfo.Version}", GetWindowStyle());

            if (!UnityInput.Current.GetKeyDown(KeyCode.Mouse0) &&
                (_windowRect.position != _windowPositionEditSetting.Value))
                SaveCurrentSizeAndPosition();

            GUI.backgroundColor = color;
        }

        private void InitializeWindow()
        {
            listEnum = null;
            listIndex = -1;
            drawerFunction = null;
            valueToSet = setting.Get();
            errorText.Clear();
            errorOnSetting = string.Empty;

            if (setting.AcceptableValueRange.Key != null)
                drawerFunction = DrawRangeField;
            else if (setting.AcceptableValues != null)
                SetAcceptableValuesDrawer();
            else if (typeof(IList<string>).IsAssignableFrom(setting.SettingType))
                drawerFunction = DrawStringListField;
            else if (setting.SettingType.IsEnum && setting.SettingType != typeof(KeyCode))
            {
                listEnum = Enum.GetValues(setting.SettingType);
                if (setting.SettingType.GetCustomAttributes(typeof(FlagsAttribute), false).Any())
                    drawerFunction = DrawFlagsField;
                else
                    drawerFunction = DrawEnumListField;
            }
            else
                SettingDrawHandlers.TryGetValue(setting.SettingType, out drawerFunction);
            
            InitListIndex();
        }

        private void InitListIndex()
        {
            listIndex = listEnum == null ? -1 : listEnum.IndexOf(valueToSet);
        }

        private void SetAcceptableValuesDrawer()
        {
            if (setting.AcceptableValues.Length == 0)
            {
                errorText.AppendLine("AcceptableValueListAttribute returned an empty list of acceptable values.");
                return;
            }

            if (!setting.SettingType.IsInstanceOfType(setting.AcceptableValues.FirstOrDefault(x => x != null)))
            {
                errorText.AppendLine("AcceptableValueListAttribute returned a list with items of type other than the settng type itself.");
                return;
            }

            if (setting.SettingType == typeof(KeyCode))
            {
                listEnum = setting.AcceptableValues?.Length > 1 ? setting.AcceptableValues : Enum.GetValues(setting.SettingType);
                drawerFunction = DrawKeyCode;
            }
            else
                drawerFunction = DrawEnumListField;
        }

        internal void SaveCurrentSizeAndPosition()
        {
            _windowSizeEditSetting.Value = new Vector2(Mathf.Clamp(_windowRect.size.x, 200f, instance.ScreenWidth / 2), Mathf.Clamp(_windowRect.size.y, 200f, instance.ScreenHeight / 2));
            _windowPositionEditSetting.Value = new Vector2(Mathf.Clamp(_windowRect.position.x, 0f, instance.ScreenWidth - _windowSize.Value.x / 4f), Mathf.Clamp(_windowRect.position.y, 0f, instance.ScreenHeight - HeaderSize * 2));
            instance.Config.Save();
        }

        private void DrawWindow(int windowID)
        {
            var backgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = _entryBackgroundColor.Value;

            GUILayout.BeginVertical(GetBackgroundStyle());

            GUILayout.Space(1f);

            GUILayout.Label($"<b>{setting.Category}</b>", GetLabelStyle(), GUILayout.ExpandWidth(true));
            
            DrawDelimiterLine();

            GUILayout.Space(1f);

            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(false));
            GUILayout.Label($"{setting.DispName} ", GetLabelStyle(), GUILayout.ExpandWidth(false));
            GUILayout.Label($"({GetTypeRepresentation(setting.SettingType)})", GetLabelStyleInfo(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Label(setting.Description, GetLabelStyle(isDefaultValue: false), GUILayout.ExpandWidth(true));

            DrawDelimiterLine();

            GUILayout.Space(1f);

            DrawSettingValue();

            if (!errorOnSetting.IsNullOrWhiteSpace())
                GUILayout.Label(errorOnSetting, GetLabelStyle());

            GUILayout.Space(5f);

            DrawMenuButtons();

            GUILayout.EndVertical();

            GUI.backgroundColor = backgroundColor;

            GUI.DragWindow(new Rect(0, 0, _windowRect.width, HeaderSize));

            if (!SettingFieldDrawer.DrawCurrentDropdown())
                DrawTooltip(_windowRect);

            _windowRect = Utilities.Utils.ResizeWindow(windowID, _windowRect, out bool sizeChanged);

            if (sizeChanged)
                SaveCurrentSizeAndPosition();
        }

        private void DrawLabel(string label, string value)
        {
            bool drawAsBlock = !label.IsNullOrWhiteSpace() && !value.IsNullOrWhiteSpace();
            if (drawAsBlock)
                GUILayout.BeginHorizontal();

            if (!label.IsNullOrWhiteSpace())
                GUILayout.Label(value.IsNullOrWhiteSpace() ? label : label + ":", GetLabelStyle(), GUILayout.ExpandWidth(false));

            if (!value.IsNullOrWhiteSpace())
                GUILayout.Label(value, GetLabelStyleInfo(), GUILayout.ExpandWidth(true));

            if (drawAsBlock)
                GUILayout.EndHorizontal();
        }

        private void DrawInfo(string info) => DrawLabel(null, info);

        private string GetTypeRepresentation(Type type)
        {
            if (!type.IsGenericType)
                return type.Name;

            Type[] genericArgs = type.GetGenericArguments();
            string elements = string.Join(", ", genericArgs.Select(t => t.Name));
            return $"{type.Name}<{elements}>";
        }

        private void DrawMenuButtons()
        {
            GUILayout.BeginHorizontal();
            {
                var enabled = GUI.enabled;
                GUI.enabled = enabled && !IsValueToSetDefaultValue();
                DrawDefaultButton();
                GUI.enabled = enabled;

                GUILayout.Label("Press Escape to close window", GetLabelStyleInfo(), GUILayout.ExpandWidth(true));

                enabled = GUI.enabled;
                GUI.enabled = enabled && !IsEqualConfigValues(setting.SettingType, valueToSet, setting.Get());
                if (GUILayout.Button("Apply", GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    ApplySettingValue();

                GUI.enabled = enabled;

                if (GUILayout.Button(_closeText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    IsOpen = false;
            }
            GUILayout.EndHorizontal();
        }

        private void ApplySettingValue()
        {
            if (valueToSet != null)
            {
                try
                {
                    setting.Set(valueToSet);
                }
                catch (Exception e)
                {
                    errorOnSetting = e.ToString();
                }
            }
        }

        private void DrawSettingValue()
        {
            var color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            {
                if (!DrawCustomField() && !DrawKnownDrawer() && errorText.Length > 0)
                    GUILayout.Label($"Error:\n{errorText}", GetLabelStyle());
            }
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            DrawDelimiterLine();

            GUILayout.Label("Edit as text:", GetLabelStyle());

            DrawUnknownField();

            GUI.backgroundColor = color;
        }

        private static void DrawDelimiterLine()
        {
            var color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;
            GUILayout.Label("", GetDelimiterLine(), GUILayout.ExpandWidth(true), GUILayout.Height(2));
            GUI.backgroundColor = color;
        }

        private bool DrawKnownDrawer()
        {
            if (drawerFunction == null)
                return false;

            try
            {
                drawerFunction();
                return true;
            }
            catch (Exception e)
            {
                LogWarning(e);
                errorText.AppendLine($"{e.GetType().Name} - {e.Message}");
            }
            return false;
        }

        public bool DrawCustomField()
        {
            if (SettingFieldDrawer.IsSettingFailedToCustomDraw(setting))
            {
                GUILayout.Label("Error when calling custom drawer function.");
                return false;
            }

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

            GUILayout.BeginHorizontal();

            int rightColumn = instance.RightColumnWidth;
            instance.SetRightColumnWidth(Mathf.RoundToInt(_windowRect.width * 0.9f));

            try
            {
                GUI.contentColor = IsValueToSetDefaultValue() ? _fontColorValueDefault.Value : _fontColorValueChanged.Value;

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
                SettingFieldDrawer.SetSettingFailedToCustomDraw(setting, e);
                result = false;
            }
            finally
            {
                instance.SetRightColumnWidth(rightColumn);
            }

            GUILayout.EndHorizontal();

            GUI.contentColor = color;
            GUI.skin.textField.fontSize = textFieldFontSize;
            GUI.skin.textArea.fontSize = textAreaFontSize;
            GUI.skin.label.fontSize = labelFontSize;
            GUI.skin.button.fontSize = buttonFontSize;

            return result;
        }

        private void DrawRangeField()
        {
            var value = valueToSet;
            var converted = (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Key, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(setting.AcceptableValueRange.Value, CultureInfo.InvariantCulture);

            float height = GetTextStyle(setting).CalcHeight(new GUIContent(value.ToString()), 100f);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Range: ", GetLabelStyle(), GUILayout.ExpandWidth(false));
            GUILayout.Label($"{leftValue} - {rightValue}", GetLabelStyleInfo(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Height(height));

            try
            {
                float result = DrawCenteredHorizontalSlider(converted, leftValue, rightValue, height);

                if (Math.Abs(result - converted) >= Mathf.Abs(rightValue - leftValue) / Math.Pow(10, _rangePrecision.Value + 2))
                {
                    valueToSet = Convert.ChangeType(Utilities.Utils.RoundWithPrecision(result, _rangePrecision.Value), setting.SettingType, CultureInfo.InvariantCulture);
                }

                if (setting.ShowRangeAsPercent == true)
                {
                    SettingFieldDrawer.DrawCenteredLabel($"{Mathf.Abs(result - leftValue) / Mathf.Abs(rightValue - leftValue):P0}", GetLabelStyle(setting));
                }
                else
                {
                    var strVal = value.ToString().Replace(',', '.').AppendZeroIfFloat(setting.SettingType);
                    var strResult = GUILayout.TextField(strVal, GetTextStyle(setting), GUILayout.Width(50));
                    if (strResult != strVal && Utilities.Utils.TryParseFloat(strResult, out float resultVal))
                    {
                        var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                        valueToSet = Convert.ChangeType(Utilities.Utils.RoundWithPrecision(clampedResultVal, _rangePrecision.Value), setting.SettingType);
                    }
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
            GUILayout.Space(height * 0.45f);
            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GetSliderStyle(), GetThumbStyle(), GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
            return result;
        }

        private void DrawUnknownField()
        {
            // Try to use user-supplied converters
            if (setting.ObjToStr != null && setting.StrToObj != null)
            {
                var text = setting.ObjToStr(valueToSet).AppendZeroIfFloat(setting.SettingType);
                if (text.IsNullOrWhiteSpace() && setting.DefaultValue.ToString() != "")
                    GUI.backgroundColor = _fontColorValueChanged.Value;

                var result = GUILayout.TextArea(text, GetTextStyle(IsValueToSetDefaultValue()), GUILayout.ExpandWidth(true));
                if (result != text)
                    valueToSet = setting.StrToObj(result);
            }
            else
            {
                // Fall back to slow/less reliable method
                var value = valueToSet == null ? "NULL" : valueToSet.ToString().AppendZeroIfFloat(setting.SettingType);

                if (value.IsNullOrWhiteSpace() && setting.DefaultValue.ToString() != "")
                    GUI.backgroundColor = _fontColorValueChanged.Value;

                if (CanCovert(value, setting.SettingType))
                {
                    var result = GUILayout.TextArea(value, GetTextStyle(IsValueToSetDefaultValue()), GUILayout.ExpandWidth(true));
                    if (result != value)
                        try
                        {
                            valueToSet = Convert.ChangeType(result, setting.SettingType, CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            // Don't change anything if format is bad
                        }
                }
                else
                {
                    valueToSet = GUILayout.TextArea(value, GetTextStyle(IsValueToSetDefaultValue()), GUILayout.ExpandWidth(true));
                }
            }
        }

        private bool IsValueToSetDefaultValue()
        {
            return IsEqualConfigValues(setting.SettingType, valueToSet, setting.DefaultValue);
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

        internal void DrawDefaultButton()
        {
            if (setting.HideDefaultButton) return;

            var color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            bool DrawResetButton()
            {
                GUILayout.Space(5);
                return GUILayout.Button(_resetSettingText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false));
            }

            if (setting.DefaultValue != null)
            {
                if (DrawResetButton())
                {
                    valueToSet = setting.DefaultValue;
                    InitListIndex();
                }
            }
            else if (setting.SettingType.IsClass)
            {
                if (DrawResetButton())
                {
                    valueToSet = null;
                    InitListIndex();
                }
            }

            GUI.backgroundColor = color;
        }

        private void DrawStringListField()
        {
            if (SettingFieldDrawer.IsSettingFailedToStringListDraw(setting))
                return;

            Color color = GUI.backgroundColor;
            GUI.backgroundColor = _widgetBackgroundColor.Value;

            bool wasUpdated = false;
            bool locked = setting.ReadOnly is true;

            float buttonWidth = GetButtonStyle().CalcSize(new GUIContent("x")).y;

            GUILayout.BeginVertical();

            List<string> stringList = valueToSet as List<string>;
            List<string> newList = new List<string>();

            for (int i = 0; i < stringList.Count; i++)
            {
                GUILayout.BeginHorizontal();

                string val = stringList[i];

                string newVal = GUILayout.TextField(val, GetTextStyle(setting), GUILayout.ExpandWidth(true));

                if (newVal != val && !locked)
                    wasUpdated = true;

                if (GUILayout.Button("x", GetButtonStyle(), GUILayout.Width(buttonWidth)) && !locked)
                    wasUpdated = true;
                else
                    newList.Add(newVal);

                if (GUILayout.Button("+", GetButtonStyle(), GUILayout.Width(buttonWidth)) && !locked)
                {
                    wasUpdated = true;
                    newList.Add("");
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            GUILayout.EndVertical();

            GUI.backgroundColor = color;

            string log = string.Empty;
            try
            {
                valueToSet = Activator.CreateInstance(setting.SettingType, new object[] { newList });
                return;
            }
            catch (Exception e)
            {
                log += e.ToString();
            }

            try
            {
                object list = Activator.CreateInstance(setting.SettingType);
                if (list is IList<string> ilist)
                {
                    foreach (var item in newList)
                        ilist.Add(item);

                    valueToSet = ilist;
                }
                return;
            }
            catch (Exception e)
            {
                log += "\n" + e.ToString();
            }

            SettingFieldDrawer.SetSettingFailedToStringListDraw(setting, log);
        }

        private void DrawBoolField()
        {
            GUI.backgroundColor = _widgetBackgroundColor.Value;
            bool boolVal = (bool)valueToSet;

            Color color = GUI.backgroundColor;
            if (boolVal)
                GUI.backgroundColor = _enabledBackgroundColor.Value;

            bool result = GUILayout.Toggle(boolVal, boolVal ? _enabledText.Value : _disabledText.Value, GetToggleStyle(setting), GUILayout.ExpandWidth(true));
            if (result != boolVal)
                valueToSet = result;

            if (boolVal)
                GUI.backgroundColor = color;
        }

        private void DrawFlagsField()
        {
            var currentValue = Convert.ToInt64(valueToSet);
            var defaultValue = Convert.ToInt64(setting.DefaultValue);

            var allValues = Enum.GetValues(setting.SettingType).Cast<Enum>().Select(x => new { name = x.ToString(), val = Convert.ToInt64(x) }).ToArray();

            float maxWidth = _windowRect.width * 0.8f;

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
                                bool curr = (currentValue & value.val) == value.val;
                                bool defValue = (defaultValue & value.val) == value.val;

                                GUIStyle style = GetToggleStyle(curr == defValue);

                                // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                var textDimension = (int)style.CalcSize(new GUIContent(value.name)).x;
                                currentWidth += textDimension;
                                if (currentWidth > maxWidth)
                                    break;

                                GUI.changed = false;

                                var newVal = GUILayout.Toggle(curr, value.name, style, GUILayout.ExpandWidth(false));
                                if (GUI.changed)
                                {
                                    var newValue = newVal ? currentValue | value.val : currentValue & ~value.val;
                                    valueToSet = Enum.ToObject(setting.SettingType, newValue);
                                }
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                GUI.changed = false;
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
        }

        private void DrawEnumListField()
        {
            var listContent = listEnum.Cast<object>().Select(SettingFieldDrawer.ObjectToGuiContent).ToArray();

            _enumScrollPosition = GUILayout.BeginScrollView(_enumScrollPosition, false, false);
            
            try
            {
                listIndex = GUILayout.SelectionGrid(listIndex, listContent, 1, GetComboBoxStyle());
                if (listEnum != null && listIndex >= 0 && listIndex < listEnum.Count)
                    valueToSet = listEnum[listIndex];
            }
            finally
            {
                GUILayout.EndScrollView();
            }
        }

        private void DrawKeyCode()
        {
            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUILayout.Label(_shortcutKeysText.Value, GetLabelStyle(), GUILayout.ExpandWidth(true));
                GUIUtility.keyboardControl = -1;

                if (_keysToCheck == null) _keysToCheck = UnityInput.Current.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();
                foreach (var key in _keysToCheck)
                {
                    if (UnityInput.Current.GetKeyUp(key))
                    {
                        valueToSet = key;
                        _currentKeyboardShortcutToSet = null;
                        break;
                    }
                }

                if (GUILayout.Button(_cancelText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                DrawEnumListField();

                if (GUILayout.Button(new GUIContent(_shortcutKeyText.Value), GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = setting;
            }
        }

        private void DrawKeyboardShortcut()
        {
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
                        valueToSet = new KeyboardShortcut(key, _keysToCheck.Where(input.GetKey).ToArray());
                        _currentKeyboardShortcutToSet = null;
                        break;
                    }
                }

                if (GUILayout.Button(_cancelText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _currentKeyboardShortcutToSet = null;
            }
            else
            {
                if (GUILayout.Button(valueToSet.ToString(), GetButtonStyle(setting), GUILayout.ExpandWidth(true)))
                    _currentKeyboardShortcutToSet = setting;

                if (GUILayout.Button(_clearText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                {
                    valueToSet = KeyboardShortcut.Empty;
                    _currentKeyboardShortcutToSet = null;
                }
            }
        }

        private void DrawVector2()
        {
            var vector = (Vector2)valueToSet;
            var copy = vector;
            bool integerValuesOnly = (vector.x % 1 == 0) && (vector.y % 1 == 0);
            vector.x = DrawSingleVectorSlider(vector.x, "X", ((Vector2)setting.DefaultValue).x, integerValuesOnly);
            vector.y = DrawSingleVectorSlider(vector.y, "Y", ((Vector2)setting.DefaultValue).y, integerValuesOnly);
            if (vector != copy) valueToSet = vector;
        }

        private void DrawVector3()
        {
            var vector = (Vector3)valueToSet;
            var copy = vector;
            bool integerValuesOnly = (vector.x % 1 == 0) && (vector.y % 1 == 0) && (vector.z % 1 == 0);
            vector.x = DrawSingleVectorSlider(vector.x, "X", ((Vector3)setting.DefaultValue).x, integerValuesOnly);
            vector.y = DrawSingleVectorSlider(vector.y, "Y", ((Vector3)setting.DefaultValue).y, integerValuesOnly);
            vector.z = DrawSingleVectorSlider(vector.z, "Z", ((Vector3)setting.DefaultValue).z, integerValuesOnly);
            if (vector != copy) valueToSet = vector;
        }

        private void DrawVector4()
        {
            var vector = (Vector4)valueToSet;
            var copy = vector;
            bool integerValuesOnly = (vector.x % 1 == 0) && (vector.y % 1 == 0) && (vector.z % 1 == 0) && (vector.w % 1 == 0);
            vector.x = DrawSingleVectorSlider(vector.x, "X", ((Vector4)setting.DefaultValue).x, integerValuesOnly);
            vector.y = DrawSingleVectorSlider(vector.y, "Y", ((Vector4)setting.DefaultValue).y, integerValuesOnly);
            vector.z = DrawSingleVectorSlider(vector.z, "Z", ((Vector4)setting.DefaultValue).z, integerValuesOnly);
            vector.w = DrawSingleVectorSlider(vector.w, "W", ((Vector4)setting.DefaultValue).w, integerValuesOnly);
            if (vector != copy) valueToSet = vector;
        }

        private void DrawQuaternion()
        {
            var vector = (Quaternion)valueToSet;
            var copy = vector;
            bool integerValuesOnly = (vector.x % 1 == 0) && (vector.y % 1 == 0) && (vector.z % 1 == 0) && (vector.w % 1 == 0);
            vector.x = DrawSingleVectorSlider(vector.x, "X", ((Quaternion)setting.DefaultValue).x, integerValuesOnly);
            vector.y = DrawSingleVectorSlider(vector.y, "Y", ((Quaternion)setting.DefaultValue).y, integerValuesOnly);
            vector.z = DrawSingleVectorSlider(vector.z, "Z", ((Quaternion)setting.DefaultValue).z, integerValuesOnly);
            vector.w = DrawSingleVectorSlider(vector.w, "W", ((Quaternion)setting.DefaultValue).w, integerValuesOnly);
            if (vector != copy) valueToSet = vector;
        }

        private float DrawSingleVectorSlider(float setting, string label, float defaultValue, bool integerValuesOnly)
        {
            GUILayout.Label(label, GetLabelStyle(), GUILayout.ExpandWidth(false));
            int precision = _vectorDynamicPrecision.Value && integerValuesOnly ? 0 : Math.Abs(_vectorPrecision.Value);
            string value = GUILayout.TextField(setting.ToString("F" + precision, CultureInfo.InvariantCulture), GetTextStyle(setting, defaultValue), GUILayout.ExpandWidth(true)).Replace(',', '.');
            if (precision == 0 && value.EndsWith('.'))
                value = string.Concat(value, string.Empty.PadRight(Math.Abs(_vectorPrecision.Value - 1), '0'), 1);

            float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
            return x;
        }

        private void DrawColor()
        {
            Color value = (Color)valueToSet;

            GUILayout.BeginVertical();
            GUILayout.BeginVertical(GetBoxStyle());
            GUILayout.BeginHorizontal();
            bool isDefaultValue = DrawHexField(ref value, (Color)valueToSet);

            GUILayout.Space(3f);
            GUIHelper.BeginColor(value);
            GUILayout.Label(string.Empty, GUILayout.ExpandWidth(true));

            if (!ColorCache.TryGetValue(setting, out var cacheEntry))
            {
                cacheEntry = new ColorCacheEntry { Tex = new Texture2D(40, 10, TextureFormat.ARGB32, false), Last = value };
                cacheEntry.Tex.FillTexture(value);
                ColorCache[setting] = cacheEntry;
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

            DrawColorField("R", ref value, ref value.r, isDefaultValue);
            GUILayout.Space(3f);
            DrawColorField("G", ref value, ref value.g, isDefaultValue);
            GUILayout.Space(3f);
            DrawColorField("B", ref value, ref value.b, isDefaultValue);
            GUILayout.Space(3f);
            DrawColorField("A", ref value, ref value.a, isDefaultValue);

            if (value != cacheEntry.Last)
            {
                valueToSet = value;
                cacheEntry.Tex.FillTexture(value);
                cacheEntry.Last = value;
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(2f);
            GUILayout.EndVertical();
        }

        private bool DrawHexField(ref Color value, Color defaultValue)
        {
            string currentText = $"#{ColorUtility.ToHtmlStringRGBA(value)}";
            string textValue = GUILayout.TextField(currentText, GetTextStyle(value, defaultValue), GUILayout.MaxWidth(Mathf.Clamp(95f * fontSize / 14, 80f, 180f)), GUILayout.ExpandWidth(false));
            if (textValue != currentText && ColorUtility.TryParseHtmlString(textValue, out Color color))
                value = color;

            return IsEqualColorConfig(value, defaultValue);
        }

        private void DrawColorField(string fieldLabel, ref Color settingColor, ref float settingValue, bool isDefaultValue)
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
