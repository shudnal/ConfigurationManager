using UnityEngine;
using static ConfigurationManager.ConfigurationManager;

namespace ConfigurationManager
{
    public class ConfigurationManagerStyles
    {
        private static GUIStyle windowStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle labelStyleValueDefault;
        private static GUIStyle labelStyleValueChanged;
        private static GUIStyle textStyleValueDefault;
        private static GUIStyle textStyleValueChanged;
        private static GUIStyle toggleStyle;
        private static GUIStyle toggleStyleValueDefault;
        private static GUIStyle toggleStyleValueChanged;
        private static GUIStyle buttonStyle;
        private static GUIStyle buttonStyleValueDefault;
        private static GUIStyle buttonStyleValueChanged;
        private static GUIStyle comboBoxStyle;
        private static GUIStyle boxStyle;
        private static GUIStyle sliderStyle;
        private static GUIStyle thumbStyle;
        private static GUIStyle categoryHeaderStyleDefault;
        private static GUIStyle categoryHeaderStyleChanged;
        private static GUIStyle pluginHeaderStyle;
        private static GUIStyle backgroundStyle;
        private static GUIStyle tooltipStyle;
        public static int fontSize = 14;
        
        public static void CreateStyles()
        {
            _textSize.Value = Mathf.Clamp(_textSize.Value, 10, 30);
            if (fontSize != _textSize.Value)
            {
                fontSize = _textSize.Value;
                SettingFieldDrawer.ClearCache();
            }

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.textColor = _fontColor.Value;
            windowStyle.fontSize = fontSize;
            windowStyle.onNormal.textColor = _fontColor.Value;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = _fontColor.Value;
            labelStyle.fontSize = fontSize;

            labelStyleValueDefault = new GUIStyle(GUI.skin.label);
            labelStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;
            labelStyleValueDefault.fontSize = fontSize;

            labelStyleValueChanged = new GUIStyle(GUI.skin.label);
            labelStyleValueChanged.normal.textColor = _fontColorValueChanged.Value;
            labelStyleValueChanged.fontSize = fontSize;

            textStyleValueDefault = new GUIStyle(GUI.skin.textArea);
            textStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;
            textStyleValueDefault.fontSize = fontSize;

            textStyleValueChanged = new GUIStyle(GUI.skin.textArea);
            textStyleValueChanged.name += "changed";
            textStyleValueChanged.normal.textColor = _fontColorValueChanged.Value;
            textStyleValueChanged.fontSize = fontSize;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = _fontColor.Value;
            buttonStyle.onNormal.textColor = _fontColor.Value;
            buttonStyle.fontSize = fontSize;

            buttonStyleValueDefault = new GUIStyle(GUI.skin.button);
            buttonStyleValueDefault.name += "default";
            buttonStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;
            buttonStyleValueDefault.onNormal.textColor = _fontColorValueDefault.Value;
            buttonStyleValueDefault.fontSize = fontSize;

            buttonStyleValueChanged = new GUIStyle(GUI.skin.button);
            buttonStyleValueChanged.name += "changed";
            buttonStyleValueChanged.normal.textColor = _fontColorValueChanged.Value;
            buttonStyleValueChanged.onNormal.textColor = _fontColorValueChanged.Value;
            buttonStyleValueChanged.fontSize = fontSize;

            categoryHeaderStyleDefault = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                stretchWidth = true
            };

            categoryHeaderStyleChanged = new GUIStyle(categoryHeaderStyleDefault);
            categoryHeaderStyleChanged.normal.textColor = _fontColorValueChanged.Value;
            categoryHeaderStyleChanged.onNormal.textColor = _fontColorValueChanged.Value;

            pluginHeaderStyle = new GUIStyle(categoryHeaderStyleDefault);

            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = _fontColor.Value;
            toggleStyle.onNormal.textColor = _fontColor.Value;
            toggleStyle.fontSize = fontSize;

            toggleStyleValueDefault = new GUIStyle(toggleStyle);
            toggleStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;
            toggleStyleValueDefault.onNormal.textColor = _fontColorValueDefault.Value;

            toggleStyleValueChanged = new GUIStyle(toggleStyle);
            toggleStyleValueChanged.name += "changed";
            toggleStyleValueChanged.normal.textColor = _fontColorValueChanged.Value;
            toggleStyleValueChanged.onNormal.textColor = _fontColorValueChanged.Value;

            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.textColor = _fontColor.Value;
            boxStyle.onNormal.textColor = _fontColor.Value;
            boxStyle.fontSize = fontSize;

            comboBoxStyle = new GUIStyle(GUI.skin.button);
            comboBoxStyle.normal = boxStyle.normal;
            comboBoxStyle.normal.textColor = _fontColorValueDefault.Value;
            comboBoxStyle.hover.background = comboBoxStyle.normal.background;
            comboBoxStyle.hover.textColor = _fontColorValueChanged.Value;
            comboBoxStyle.fontSize = fontSize;
            comboBoxStyle.border = boxStyle.border;
            comboBoxStyle.stretchHeight = true;
            comboBoxStyle.padding.top = 1;
            comboBoxStyle.padding.bottom = 1;
            comboBoxStyle.margin.top = 0;
            comboBoxStyle.margin.bottom = 0;

            backgroundStyle = new GUIStyle(GUI.skin.box);
            backgroundStyle.normal.textColor = _fontColor.Value;
            backgroundStyle.fontSize = fontSize;
            backgroundStyle.normal.background = EntryBackground;

            tooltipStyle = new GUIStyle(GUI.skin.box);
            tooltipStyle.normal.textColor = _fontColor.Value;
            tooltipStyle.fontSize = fontSize;
            tooltipStyle.wordWrap = true;
            tooltipStyle.alignment = TextAnchor.MiddleCenter;
            tooltipStyle.normal.background = TooltipBackground;

            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);

            thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
        }

        public static GUIStyle GetWindowStyle()
        {
            return windowStyle;
        }

        public static GUIStyle GetCategoryStyle(bool isDefaultStyle = true)
        {
            return isDefaultStyle ? categoryHeaderStyleDefault : categoryHeaderStyleChanged;
        }

        public static GUIStyle GetHeaderStyle()
        {
            return pluginHeaderStyle;
        }

        public static GUIStyle GetSliderStyle()
        {
            return sliderStyle;
        }

        public static GUIStyle GetThumbStyle()
        {
            return thumbStyle;
        }

        public static GUIStyle GetBoxStyle()
        {
            return boxStyle;
        }

        public static GUIStyle GetTooltipStyle()
        {
            return tooltipStyle;
        }

        public static GUIStyle GetBackgroundStyle()
        {
            return backgroundStyle;
        }

        public static GUIStyle GetComboBoxStyle()
        {
            return comboBoxStyle;
        }

        public static GUIStyle GetToggleStyle()
        {
            return toggleStyle;
        }

        public static GUIStyle GetToggleStyle(bool isDefaulValue = true)
        {
            return isDefaulValue ? toggleStyleValueDefault : toggleStyleValueChanged;
        }

        public static GUIStyle GetToggleStyle(SettingEntryBase setting)
        {
            return GetToggleStyle(IsDefaultValue(setting));
        }

        public static GUIStyle GetLabelStyle()
        {
            return labelStyle;
        }

        public static GUIStyle GetLabelStyle(bool isDefaulValue = true)
        {
            return isDefaulValue ? labelStyleValueDefault : labelStyleValueChanged;
        }
        
        public static GUIStyle GetLabelStyle(SettingEntryBase setting)
        {
            return GetLabelStyle(IsDefaultValue(setting));
        }

        public static GUIStyle GetButtonStyle()
        {
            return buttonStyle;
        }

        public static GUIStyle GetButtonStyle(bool isDefaulValue = true)
        {
            return isDefaulValue ? buttonStyleValueDefault : buttonStyleValueChanged;
        }
        
        public static GUIStyle GetButtonStyle(SettingEntryBase setting)
        {
            return GetButtonStyle(IsDefaultValue(setting));
        }

        public static GUIStyle GetTextStyle(bool isDefaulValue = true)
        {
            return isDefaulValue ? textStyleValueDefault : textStyleValueChanged;
        }

        public static GUIStyle GetTextStyle(SettingEntryBase setting)
        {
            return GetTextStyle(IsDefaultValue(setting));
        }

        public static GUIStyle GetTextStyle(float setting, float defaultValue)
        {
            return GetTextStyle(setting == defaultValue);
        }

        public static GUIStyle GetTextStyle(Color setting, Color defaultValue)
        {
            return GetTextStyle(IsEqualColorConfig(setting, defaultValue));
        }

        public static bool IsEqualColorConfig(Color setting, Color defaultValue)
        {
            return ColorUtility.ToHtmlStringRGBA(setting) == ColorUtility.ToHtmlStringRGBA(defaultValue);
        }

        internal static bool IsDefaultValue(SettingEntryBase setting)
        {
            if (setting == null || setting.DefaultValue == null || setting.Get() == null)
                return true;

            try
            {
                return setting.SettingType == typeof(Color) ? IsEqualColorConfig((Color)setting.Get(), (Color)setting.DefaultValue) : setting.Get().ToString() == setting.DefaultValue.ToString();
            }
            catch
            {
                return true;
            }
        }
    }
}
