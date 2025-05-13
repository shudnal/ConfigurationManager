using BepInEx;
using BepInEx.Configuration;
using ConfigurationManager.Utilities;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static ConfigurationManager.ConfigurationManager;
using static ConfigurationManager.ConfigurationManagerStyles;

namespace ConfigurationManager
{
    internal class SettingEditWindow
    {
        private Rect _windowRect = new Rect(_windowPositionEditSetting.Value, _windowSizeEditSetting.Value);

        private const int WindowId = -6800;
        private const string NewItemFieldControlName = "StringListNewItemField";
        private SettingEntryBase setting;

        private static SettingEntryBase _currentKeyboardShortcutToSet;
        public Dictionary<Type, Action> SettingDrawHandlers { get; }
        
        private static IEnumerable<KeyCode> _keysToCheck;

        private static readonly Dictionary<SettingEntryBase, ColorCacheEntry> ColorCache = new Dictionary<SettingEntryBase, ColorCacheEntry>();

        private Vector2 _scrollPosition = Vector2.zero;
        private Vector2 _scrollPositionEnum = Vector2.zero;

        private int listIndex = -1;
        private IList listEnum = null;

        private Action drawerFunction;

        private string errorText;

        private object valueToSet;

        private string errorOnSetting;

        private readonly List<string> vectorParts = new List<string>();
        private readonly List<float> vectorFloats = new List<float>();
        private readonly List<float> vectorDefault = new List<float>();

        private string colorAsHEX;

        private List<string> separatedStringDefault = new List<string>();
        private List<string> separatedString = new List<string>();
        private string separator;
        private int editStringView;
        private string newItem;

        private ConfigEntryBase dummyCustomDrawerConfigEntry;

        private bool IsStringList => setting != null && setting.SettingType != null && typeof(IList<string>).IsAssignableFrom(setting.SettingType);

        public SettingEditWindow()
        {
            SettingDrawHandlers = new Dictionary<Type, Action>
            {
                { typeof(bool), DrawBoolField},
                { typeof(KeyboardShortcut), DrawKeyboardShortcut},
                { typeof(KeyCode), DrawKeyCode},
                { typeof(Color), DrawColor },
                { typeof(Vector2), DrawVector },
                { typeof(Vector3), DrawVector },
                { typeof(Vector4), DrawVector },
                { typeof(Quaternion), DrawVector },
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

        private void UpdateStringList()
        {
            if (separator.IsNullOrWhiteSpace())
                separator = ",";

            if (setting.SettingType != typeof(string) && !IsStringList)
                return;

            separatedString.Clear();
            if (IsStringList)
                try
                {
                    separatedString.AddRange(valueToSet as IList<string>);
                }
                catch
                {
                    separatedString.AddRange(valueToSet.ToString().Split(separator));
                }
            else
                separatedString.AddRange(valueToSet.ToString().Split(separator));

            separatedStringDefault.Clear();
            if (setting.DefaultValue != null)
                if (IsStringList)
                    try
                    {
                        separatedStringDefault.AddRange((setting.DefaultValue as IList<string>).Select(s => s.Trim()));
                    }
                    catch
                    {
                        separatedStringDefault.AddRange(setting.DefaultValue.ToString().Split(separator).Select(s => s.Trim()));
                    }
                else
                    separatedStringDefault.AddRange(setting.DefaultValue.ToString().Split(separator).Select(s => s.Trim()));
        }

        private void InitializeWindow()
        {
            listEnum = null;
            listIndex = -1;
            drawerFunction = null;
            valueToSet = setting.SettingType == typeof(Color) ? Utilities.Utils.RoundColorToHEX((Color)setting.Get()) : setting.Get();
            errorText = string.Empty;
            errorOnSetting = string.Empty;
            colorAsHEX = setting.SettingType == typeof(Color) ? $"#{ColorUtility.ToHtmlStringRGBA((Color)valueToSet)}" : string.Empty;
            separator = ",";
            separatedString.Clear();
            newItem = string.Empty;
            editStringView = 0;
            _scrollPosition = Vector2.zero;
            _scrollPositionEnum = Vector2.zero;

            dummyCustomDrawerConfigEntry = null;
            if (setting is ConfigSettingEntry newSetting && (setting.CustomDrawer != null || setting.CustomHotkeyDrawer != null))
            {
                // Create dummy entry to not change original config entry
                Type genericType = typeof(ConfigEntry<>).MakeGenericType(newSetting.Entry.SettingType);
                ConstructorInfo constructor = AccessTools.Constructor(genericType, new[] {
                    typeof(ConfigFile),
                    typeof(ConfigDefinition),
                    newSetting.Entry.SettingType,
                    typeof(ConfigDescription)
                });

                dummyCustomDrawerConfigEntry = (ConfigEntryBase)constructor.Invoke(new object[] {
                    newSetting.Entry.ConfigFile,
                    newSetting.Entry.Definition,
                    newSetting.Entry.DefaultValue,
                    newSetting.Entry.Description
                });
                dummyCustomDrawerConfigEntry.BoxedValue = newSetting.Entry.BoxedValue;
            }

            if (setting.AcceptableValueRange.Key != null)
                drawerFunction = DrawRangeField;
            else if (setting.AcceptableValues != null && setting.AcceptableValues.Length > 0 && setting.SettingType.IsInstanceOfType(setting.AcceptableValues.FirstOrDefault(x => x != null)))
                SetAcceptableValuesDrawer();
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

            InitVectorParts();
            
            UpdateStringList();
        }

        private void InitListIndex()
        {
            listIndex = listEnum == null ? -1 : listEnum.IndexOf(valueToSet);
        }

        private void InitVectorParts()
        {
            vectorParts.Clear();
            vectorFloats.Clear();
            vectorDefault.Clear();

            if (setting.SettingType == typeof(Vector2))
            {
                FillVectorList(vectorFloats, (Vector2)valueToSet);
                FillVectorList(vectorDefault, (Vector2)setting.DefaultValue);
            }
            else if (setting.SettingType == typeof(Vector3))
            {
                FillVectorList(vectorFloats, (Vector3)valueToSet);
                FillVectorList(vectorDefault, (Vector3)setting.DefaultValue);
            }
            else if (setting.SettingType == typeof(Vector4))
            {
                FillVectorList(vectorFloats, (Vector4)valueToSet);
                FillVectorList(vectorDefault, (Vector4)setting.DefaultValue);
            }
            else if (setting.SettingType == typeof(Quaternion))
            {
                FillVectorList(vectorFloats, (Quaternion)valueToSet);
                FillVectorList(vectorDefault, (Quaternion)setting.DefaultValue);
            }

            vectorParts.AddRange(vectorFloats.Select(f => f.ToString()));
        }

        private void SetAcceptableValuesDrawer()
        {
            if (setting.SettingType == typeof(KeyCode))
            {
                listEnum = setting.AcceptableValues.Length > 1 ? setting.AcceptableValues : Enum.GetValues(setting.SettingType);
                drawerFunction = DrawKeyCode;
            }
            else
            {
                listEnum = setting.AcceptableValues;
                drawerFunction = DrawEnumListField;
            }
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

            GUILayout.BeginVertical(GetSettingWindowBackgroundStyle());

            GUILayout.Space(1f);

            GUILayout.Label($"<b>{setting.Category}</b>", GetLabelStyle(), GUILayout.ExpandWidth(true));
            
            DrawDelimiterLine();

            GUILayout.Space(1f);

            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(false));
            GUILayout.Label($"{setting.DispName} ", GetLabelStyle(), GUILayout.ExpandWidth(false));
            GUILayout.Label($"({GetTypeRepresentation(setting.SettingType)})", GetLabelStyleInfo(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Label(setting.Description, GetLabelStyle(isDefaultValue: false), GUILayout.ExpandWidth(true));

            if (setting.DefaultValue != null)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandHeight(false));
                GUILayout.Label(_defaultValueDescriptionEditWindow.Value, GetLabelStyle(), GUILayout.ExpandWidth(false));
                GUILayout.Label($"{GetValueRepresentation(setting.DefaultValue, setting.SettingType)}", GetLabelStyleInfo(), GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }

            DrawDelimiterLine();

            GUILayout.Space(5f);

            DrawSettingValue();

            if (!errorOnSetting.IsNullOrWhiteSpace())
                GUILayout.Label(errorOnSetting, GetLabelStyle());

            DrawDelimiterLine();

            GUILayout.Space(1f);

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

        private static readonly Dictionary<Type, string> typeMappings = new Dictionary<Type, string>
        {
            { typeof(int), "Integer" },
            { typeof(float), "Float" },
            { typeof(double), "Double" },
            { typeof(decimal), "Decimal" },
            { typeof(bool), "Boolean" },
            { typeof(string), "String" },
            { typeof(long), "Long" },
            { typeof(short), "Short" },
            { typeof(byte), "Byte" },
            { typeof(sbyte), "Signed Byte" },
            { typeof(uint), "Unsigned Integer" },
            { typeof(ulong), "Unsigned Long" },
            { typeof(ushort), "Unsigned Short" },
            { typeof(char), "Character" },
            { typeof(DateTime), "DateTime" },
            { typeof(TimeSpan), "TimeSpan" },
            { typeof(Guid), "GUID" },
            { typeof(KeyValuePair<,>), "Map<Key, Value>" },
            { typeof(object), "Object" },
        };

        private string GetTypeRepresentation(Type type)
        {
            if (!type.IsGenericType)
                return typeMappings.TryGetValue(type, out string friendlyName) ? friendlyName : type.Name;

            Type[] genericArgs = type.GetGenericArguments();
            string elements = string.Join(", ", genericArgs.Select(t => typeMappings.TryGetValue(t, out string name) ? name : t.Name));
            return $"{typeMappings.GetValueOrDefault(type, type.Name)}<{elements}>";
        }

        private string GetValueRepresentation(object value, Type type)
        {
            if (type == typeof(Color))
                return $"#{ColorUtility.ToHtmlStringRGBA((Color)value)}";
            else if (type == typeof(bool))
                return (bool)value ? _enabledText.Value : _disabledText.Value;

            return value.ToString();
        }

        private void DrawMenuButtons()
        {
            GUILayout.BeginHorizontal();
            {
                var enabled = GUI.enabled;
                GUI.enabled = enabled && !IsValueToSetDefaultValue();
                DrawDefaultButton();
                GUI.enabled = enabled;

                GUILayout.Label(_pressEscapeHintEditWindow.Value, GetLabelStyleInfo(), GUILayout.ExpandWidth(true));

                enabled = GUI.enabled;
                GUI.enabled = enabled && !IsEqualConfigValues(setting.SettingType, valueToSet, setting.Get());
                if (GUILayout.Button(_applyButtonEditWindow.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
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

            bool drawStringMenu = false;
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            {
                if (!DrawCustomField() && !DrawKnownDrawer())
                {
                    if (errorText.Length > 0)
                        GUILayout.Label($"Error:\n{errorText}", GetLabelStyle());
                    
                    DrawUnknownField(out drawStringMenu);
                }
            }
            GUILayout.EndScrollView();

            if (drawStringMenu)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(_editAsLabelEditWindow.Value, GetLabelStyle(), GUILayout.ExpandWidth(false));
                if (editStringView != (editStringView = GUILayout.SelectionGrid(editStringView, new[] { _editAsTextEditWindow.Value, _editAsListEditWindow.Value }, 2, GetButtonStyle(), GUILayout.ExpandWidth(false))))
                {
                    // On view mode change?
                }

                if (editStringView > 0)
                {
                    GUILayout.Label(_separatorLabelEditWindow.Value, GetLabelStyle(), GUILayout.ExpandWidth(false));
                    if (separator != (separator = GUILayout.TextField(separator)))
                        UpdateStringList();

                    if (GUILayout.Button(_trimWhitespaceButtonEditWindow.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    {
                        separatedString = separatedString.Select(s => s.Trim()).ToList();
                        valueToSet = setting.StrToObj(string.Join(separator, separatedString));
                    }
                }
                GUILayout.EndHorizontal();
            }

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
                errorText = $"{e.GetType().Name} - {e.Message}";
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

            // Some plugins rely on this field to limit width
            int rightColumn = instance.RightColumnWidth;
            instance.SetRightColumnWidth(Mathf.RoundToInt(_windowRect.width * 0.9f));

            try
            {
                GUI.contentColor = IsValueToSetDefaultValue() ? _fontColorValueDefault.Value : _fontColorValueChanged.Value;

                if (setting.CustomDrawer != null)
                    setting.CustomDrawer(dummyCustomDrawerConfigEntry);
                else if (setting.CustomHotkeyDrawer != null)
                {
                    var isBeingSet = _currentKeyboardShortcutToSet == setting;
                    var isBeingSetOriginal = isBeingSet;
                    setting.CustomHotkeyDrawer(dummyCustomDrawerConfigEntry, ref isBeingSet);

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

            if (result && dummyCustomDrawerConfigEntry != null)
                valueToSet = dummyCustomDrawerConfigEntry.BoxedValue;

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
            GUILayout.Label(_rangeLabelEditWindow.Value, GetLabelStyle(), GUILayout.ExpandWidth(false));
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
            GUILayout.Space(height * 0.35f);
            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GetSliderStyle(), GetThumbStyle(), GUILayout.ExpandWidth(true), GUILayout.Height(height));
            GUILayout.EndVertical();
            return result;
        }

        private void DrawUnknownField(out bool drawStringMenu)
        {
            drawStringMenu = false;

            // Try to use user-supplied converters
            if (setting.ObjToStr != null && setting.StrToObj != null)
            {
                string text = setting.ObjToStr(valueToSet).AppendZeroIfFloat(setting.SettingType);

                if (setting.SettingType == typeof(string) || IsStringList)
                {
                    if (editStringView > 0)
                    {
                        DrawEditableList(); 
                        valueToSet = setting.StrToObj(string.Join(separator, separatedString));
                        GUILayout.FlexibleSpace();
                    }
                    else
                    {
                        string result = GUILayout.TextArea(text, GetTextStyle(IsValueToSetDefaultValue()), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        if (result != text)
                            valueToSet = setting.StrToObj(result);
                    }
                    drawStringMenu = true;
                }
                else
                {
                    string result = GUILayout.TextArea(text, GetTextStyle(IsValueToSetDefaultValue()), GUILayout.ExpandWidth(true));
                    if (result != text)
                        valueToSet = setting.StrToObj(result);
                }
            }
            else
            {
                // Fall back to slow/less reliable method
                var value = valueToSet == null ? "NULL" : valueToSet.ToString().AppendZeroIfFloat(setting.SettingType);

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

        private void DrawEditableList()
        {
            float width = Mathf.Round(GetButtonStyle().CalcSize(new GUIContent("▲")).x);

            for (int i = 0; i < separatedString.Count; i++)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("✕", GetButtonStyle(), GUILayout.Width(width)))
                {
                    GUILayout.EndHorizontal();
                    separatedString.RemoveAt(i);
                    break;
                }

                separatedString[i] = GUILayout.TextArea(separatedString[i], GetTextStyle(separatedStringDefault.IndexOf(separatedString[i].Trim()) == i), GUILayout.ExpandWidth(true));

                var enabled = GUI.enabled;
                GUI.enabled = i > 0;
                if (GUILayout.Button("▲", GetButtonStyle(), GUILayout.Width(width)))
                    SwapElements(i, i - 1);

                GUI.enabled = i < separatedString.Count - 1;
                if (GUILayout.Button("▼", GetButtonStyle(), GUILayout.Width(width)))
                    SwapElements(i, i + 1);

                GUI.enabled = enabled;

                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();

            GUI.SetNextControlName(NewItemFieldControlName);
            newItem = GUILayout.TextField(newItem, GetTextStyle(isDefaultValue:false), GUILayout.ExpandWidth(true));

            if (string.IsNullOrEmpty(newItem) && Event.current.type == EventType.Repaint)
                GUI.Label(GUILayoutUtility.GetLastRect(), _newValuePlaceholderEditWindow.Value, GetPlaceholderTextStyle());

            if (GUILayout.Button(_addButtonEditWindow.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)) && !string.IsNullOrWhiteSpace(newItem))
            {
                separatedString.Add(newItem);
                newItem = "";
                GUI.FocusControl(NewItemFieldControlName);
            }

            GUILayout.EndHorizontal();

            void SwapElements(int indexA, int indexB) => (separatedString[indexA], separatedString[indexB]) = (separatedString[indexB], separatedString[indexA]);
        }

        private bool IsValueToSetDefaultValue() => IsEqualConfigValues(setting.SettingType, valueToSet, setting.DefaultValue);

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

        public static void ClearCache()
        {
            foreach (var tex in ColorCache)
                UnityEngine.Object.Destroy(tex.Value.Tex);
            ColorCache.Clear();
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
                    valueToSet = setting.SettingType == typeof(Color) ? Utilities.Utils.RoundColorToHEX((Color)setting.DefaultValue) : setting.DefaultValue;
                    if (dummyCustomDrawerConfigEntry != null)
                        dummyCustomDrawerConfigEntry.BoxedValue = setting.DefaultValue;
                    InitListIndex();
                    InitVectorParts();
                    UpdateStringList();
                    ClearCache();
                }
            }
            else if (setting.SettingType.IsClass)
            {
                if (DrawResetButton())
                {
                    valueToSet = null;
                    if (dummyCustomDrawerConfigEntry != null)
                        dummyCustomDrawerConfigEntry.BoxedValue = null;
                    InitListIndex();
                    InitVectorParts();
                    UpdateStringList();
                    ClearCache();
                }
            }

            GUI.backgroundColor = color;
        }

        private void DrawBoolField()
        {
            GUI.backgroundColor = _widgetBackgroundColor.Value;
            bool boolVal = (bool)valueToSet;

            Color color = GUI.backgroundColor;
            if (boolVal)
                GUI.backgroundColor = _enabledBackgroundColor.Value;

            bool result = GUILayout.SelectionGrid(boolVal ? 0 : 1, new [] {_enabledText.Value, _disabledText.Value }, 2, GetButtonStyle(), GUILayout.ExpandWidth(false)) == 0;
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

                                GUIStyle style = GetButtonStyle(curr == defValue);

                                // Make sure this horizontal group doesn't extend over window width, if it does then start a new horiz group below
                                var textDimension = (int)style.CalcSize(new GUIContent(value.name)).x;
                                currentWidth += textDimension;
                                if (currentWidth > maxWidth)
                                    break;

                                GUI.changed = false;

                                if (GUILayout.Button(value.name, style, GUILayout.ExpandWidth(false)))
                                    curr = !curr;

                                if (GUI.changed)
                                {
                                    var newValue = curr ? currentValue | value.val : currentValue & ~value.val;
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

            _scrollPositionEnum = GUILayout.BeginScrollView(_scrollPositionEnum, false, false);
            
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

            GUILayout.FlexibleSpace();
        }

        private void DrawKeyCode()
        {
            if (ReferenceEquals(_currentKeyboardShortcutToSet, setting))
            {
                GUILayout.Label(_shortcutKeysText.Value, GetLabelStyle(), GUILayout.ExpandWidth(true));
                GUIUtility.keyboardControl = -1;

                _keysToCheck ??= UnityInput.Current.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();
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
                listEnum ??= Enum.GetValues(setting.SettingType);

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
                _keysToCheck ??= input.SupportedKeyCodes.Except(new[] { KeyCode.Mouse0, KeyCode.None }).ToArray();
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

        private void DrawVectorPart(int position)
        {
            string label = position switch
            {
                0 => "X",
                1 => "Y",
                2 => "Z",
                3 => "W",
                _ => ""
            };

            bool isDefaultValue = float.TryParse(vectorParts[position], NumberStyles.Any, CultureInfo.InvariantCulture, out var x) && vectorDefault[position] == x;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} ", GetLabelStyle(), GUILayout.ExpandWidth(false));
            vectorParts[position] = GUILayout.TextField(vectorParts[position], GetTextStyle(isDefaultValue), GUILayout.ExpandWidth(true)).Replace(',', '.');
            GUILayout.EndHorizontal();
        }

        private void DrawVector()
        {
            for (int i = 0; i < vectorParts.Count; i++)
                DrawVectorPart(i);

            for (int i = 0; i < vectorParts.Count; i++)
                if (float.TryParse(vectorParts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out var x))
                    vectorFloats[i] = x;

            if (setting.SettingType == typeof(Vector2))
                valueToSet = new Vector2(vectorFloats[0], vectorFloats[1]);
            else if (setting.SettingType == typeof(Vector3))
                valueToSet = new Vector3(vectorFloats[0], vectorFloats[1], vectorFloats[2]);
            else if (setting.SettingType == typeof(Vector4))
                valueToSet = new Vector4(vectorFloats[0], vectorFloats[1], vectorFloats[2], vectorFloats[3]);
            else if (setting.SettingType == typeof(Quaternion))
                valueToSet = new Quaternion(vectorFloats[0], vectorFloats[1], vectorFloats[2], vectorFloats[3]);

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{_precisionLabelEditWindow.Value}: {_vectorPrecision.Value} ", GetLabelStyle(), GUILayout.ExpandWidth(false));
            float height = GetTextStyle(setting).CalcHeight(new GUIContent(_vectorPrecision.Value.ToString()), 100f);
            _vectorPrecision.Value = Mathf.RoundToInt(DrawCenteredHorizontalSlider(_vectorPrecision.Value, 0f, 5f, height));
            GUILayout.EndHorizontal();
        }

        private void DrawColor()
        {
            Color value = (Color)valueToSet;
            Color defaultColor = Utilities.Utils.RoundColorToHEX((Color)setting.DefaultValue);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            DrawHexField(ref value, defaultColor);

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

            DrawColorField("Red", ref value, ref value.r, Utilities.Utils.RoundColor(value.r) == Utilities.Utils.RoundColor(defaultColor.r));
            DrawColorField("Green", ref value, ref value.g, Utilities.Utils.RoundColor(value.g) == Utilities.Utils.RoundColor(defaultColor.g));
            DrawColorField("Blue", ref value, ref value.b, Utilities.Utils.RoundColor(value.b) == Utilities.Utils.RoundColor(defaultColor.b));
            DrawColorField("Alpha", ref value, ref value.a, Utilities.Utils.RoundColor(value.a) == Utilities.Utils.RoundColor(defaultColor.a));

            HSLColor defaultHSL = defaultColor;
            HSLColor hsl = value;
            DrawHSLField("Hue", ref hsl, ref hsl.h, Utilities.Utils.RoundWithPrecision(hsl.h, 1) == Utilities.Utils.RoundWithPrecision(defaultHSL.h, 1));
            DrawHSLField("Saturation", ref hsl, ref hsl.s, Utilities.Utils.RoundColor(hsl.s) == Utilities.Utils.RoundColor(defaultHSL.s));
            DrawHSLField("Lightness", ref hsl, ref hsl.l, Utilities.Utils.RoundColor(hsl.l) == Utilities.Utils.RoundColor(defaultHSL.l));

            value = hsl;

            if (value != cacheEntry.Last)
            {
                valueToSet = value;
                cacheEntry.Tex.FillTexture(value);
                cacheEntry.Last = value;
                colorAsHEX = $"#{ColorUtility.ToHtmlStringRGBA(value)}";
            }

            GUILayout.EndVertical();
        }

        private bool DrawHexField(ref Color value, Color defaultValue)
        {
            GUIStyle style = GetTextStyle(value, defaultValue);
            UpdateHexString(ref colorAsHEX, GUILayout.TextField(colorAsHEX, style, GUILayout.Width(style.CalcSize(new GUIContent("#CCCCCCCC.")).x), GUILayout.ExpandWidth(false)));

            bool enabled = GUI.enabled;
            GUI.enabled = !colorAsHEX.Replace("#", "").Equals(ColorUtility.ToHtmlStringRGBA(value), StringComparison.OrdinalIgnoreCase);

            if (GUILayout.Button(_shortcutKeyText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)) && ColorUtility.TryParseHtmlString(colorAsHEX, out Color color))
                value = color;

            GUI.enabled = enabled;

            return IsEqualColorConfig(value, defaultValue);
        }

        void UpdateHexString(ref string originalHEX, string newHEX)
        {
            char[] hexChars = (newHEX.StartsWith("#") ? newHEX : "#" + newHEX).ToUpper().ToCharArray();
            for (int i = 1; i < hexChars.Length; i++)
                if (!IsValidHexChar(hexChars[i]))
                    hexChars[i] = 'F';

            newHEX = new string(hexChars);

            if (originalHEX.Equals(newHEX))
                return;

            //LogInfo($"\n\"{originalHEX}\" -> \"{newHEX}\"");

            if (originalHEX.Length == newHEX.Length)
            {
                // Symbols were replaced without change of string length, just replace string
                originalHEX = newHEX;
            }
            else if (originalHEX.IndexOf(newHEX) == 0)
            {
                // Symbols were removed from tail
                originalHEX = newHEX.PadRight(9, '0');
            }
            else if (newHEX.IndexOf(originalHEX) == 0)
            {
                // Extra symbols were added, ignore
                originalHEX = newHEX.Substring(0, 9);
            }
            else if (originalHEX.Length > newHEX.Length)
            {
                FindStartEndLength(originalHEX, newHEX, out string startString, out string endString);

                // Symbols were removed, concat start + zeroes + end
                originalHEX = startString + new string('0', originalHEX.Length - newHEX.Length) + endString;
            }
            else if (originalHEX.Length < newHEX.Length)
            {
                FindStartEndLength(originalHEX, newHEX, out string startString, out string endString);

                // Symbols were added, replace and follow with original string
                int replacedLength = newHEX.Length - endString.Length - startString.Length;
                originalHEX = newHEX.Substring(0, startString.Length + replacedLength) + newHEX.Substring(startString.Length + replacedLength + (newHEX.Length - 9));
            }

            originalHEX = originalHEX.PadRight(9, '0');

            //LogInfo($"{originalHEX}");
        }

        void FindStartEndLength(string originalHEX, string newHEX, out string startString, out string endString)
        {
            int startLength = -1;
            int endLength = -1;

            int minLength = Math.Min(originalHEX.Length, newHEX.Length);
            for (int i = 0; i < minLength; i++)
            {
                if (startLength != -1 && endLength != -1)
                    break;

                if (startLength == -1 && originalHEX[i] != newHEX[i])
                    startLength = i;

                if (endLength == -1 && originalHEX[originalHEX.Length - 1 - i] != newHEX[newHEX.Length - 1 - i])
                    endLength = minLength - 1 - i;
            }

            if (startLength == -1)
                startLength = minLength;

            if (endLength == -1)
                endLength = 0;

            endLength = minLength - endLength;

            startString = newHEX.Substring(0, startLength);
            endString = newHEX.Substring(newHEX.Length - endLength + 1);
        }

        bool IsValidHexChar(char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

        private void DrawColorField(string fieldLabel, ref Color settingColor, ref float settingValue, bool isDefaultValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(fieldLabel, GetLabelStyle(), GUILayout.Width(GetLabelStyle().CalcSize(new GUIContent("Green.")).x), GUILayout.ExpandWidth(false));

            GUIStyle style = GetTextStyle(isDefaultValue);
            Vector2 size = style.CalcSize(new GUIContent("0,000."));

            string currentText = Utilities.Utils.RoundWithPrecision(settingValue, 3).ToString("0.000");
            SetColorValue(ref settingColor, float.Parse(GUILayout.TextField(currentText, style, GUILayout.Width(size.x), GUILayout.ExpandWidth(false)).Replace('.', ',')));

            SetColorValue(ref settingColor, byte.Parse(GUILayout.TextField((Utilities.Utils.RoundWithPrecision(settingValue, 3) * 255).ToString("F0"), style, GUILayout.Width(style.CalcSize(new GUIContent("000.")).x), GUILayout.ExpandWidth(false))) / 255f);

            SetColorValue(ref settingColor, DrawCenteredHorizontalSlider(settingValue, 0f, 1f, size.y));

            GUILayout.EndHorizontal();

            void SetColorValue(ref Color color, float value)
            {
                float roundedValue = Utilities.Utils.RoundWithPrecision(value, 3);
                switch (fieldLabel)
                {
                    case "Red":     color.r = Mathf.Clamp01(roundedValue); break;
                    case "Green":   color.g = Mathf.Clamp01(roundedValue); break;
                    case "Blue":    color.b = Mathf.Clamp01(roundedValue); break;
                    case "Alpha":   color.a = Mathf.Clamp01(roundedValue); break;
                }
            }
        }

        private void DrawHSLField(string fieldLabel, ref HSLColor settingColor, ref float settingValue, bool isDefaultValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(fieldLabel, GetLabelStyle(), GUILayout.Width(GetLabelStyle().CalcSize(new GUIContent("Saturation..")).x), GUILayout.ExpandWidth(false));

            GUIStyle style = GetTextStyle(isDefaultValue);
            Vector2 size = style.CalcSize(new GUIContent("000,00."));

            string currentText = Utilities.Utils.RoundWithPrecision(settingValue, fieldLabel == "Hue" ? 2 : 4).ToString(fieldLabel == "Hue" ? "000.00" : "0.0000");
            SetColorValue(ref settingColor, float.Parse(GUILayout.TextField(currentText, style, GUILayout.Width(size.x), GUILayout.ExpandWidth(false)).Replace('.', ',')));

            SetColorValue(ref settingColor, DrawCenteredHorizontalSlider(settingValue, 0f, fieldLabel == "Hue" ? 360f : 1f, size.y));

            GUILayout.EndHorizontal();

            void SetColorValue(ref HSLColor color, float value)
            {
                float roundedValue = Utilities.Utils.RoundWithPrecision(value, fieldLabel == "Hue" ? 2 : 4);
                switch (fieldLabel)
                {
                    case "Hue":         color.h = Mathf.Clamp(roundedValue, 0f, 360f); break;
                    case "Saturation":  color.s = Mathf.Clamp01(roundedValue > 1f ? roundedValue / 100 : roundedValue); break;
                    case "Lightness":   color.l = Mathf.Clamp01(roundedValue > 1f ? roundedValue / 100 : roundedValue); break;
                }
            }
        }

        private static void FillVectorList<T>(List<float> list, T value)
        {
            if (value == null) return;

            if (value is Vector2 vector2)
            {
                list.Add(vector2.x);
                list.Add(vector2.y);
            }
            else if (value is Vector3 vector3)
            {
                list.Add(vector3.x);
                list.Add(vector3.y);
                list.Add(vector3.z);
            }
            else if (value is Vector4 vector4)
            {
                list.Add(vector4.x);
                list.Add(vector4.y);
                list.Add(vector4.z);
                list.Add(vector4.w);
            }
            else if (value is Quaternion quaternion)
            {
                list.Add(quaternion.x);
                list.Add(quaternion.y);
                list.Add(quaternion.z);
                list.Add(quaternion.w);
            }
        }

        private sealed class ColorCacheEntry
        {
            public Color Last;
            public Texture2D Tex;
        }
    }
}
