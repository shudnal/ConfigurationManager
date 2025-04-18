using System;
using UnityEngine;
using static ConfigurationManager.ConfigurationManager;

namespace ConfigurationManager
{
    public class ConfigurationManagerStyles
    {
        private static GUIStyle windowStyle;
        private static GUIStyle labelStyle;
        private static GUIStyle labelStyleSettingName;
        private static GUIStyle labelStyleInfo;
        private static GUIStyle labelStyleValueDefault;
        private static GUIStyle labelStyleValueChanged;
        private static GUIStyle textStyle;
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
        private static GUIStyle pluginHeaderStyleActive;
        private static GUIStyle pluginHeaderStyleSplitView;
        private static GUIStyle pluginHeaderStyleSplitViewActive;
        private static GUIStyle backgroundStyle;
        private static GUIStyle backgroundStyleWithHover;
        private static GUIStyle categoryBackgroundStyle;
        private static GUIStyle categoryHeaderBackgroundStyle;
        private static GUIStyle categoryHeaderBackgroundStyleWithHover;
        private static GUIStyle tooltipStyle;
        private static GUIStyle fileEditorFileStyle;
        private static GUIStyle fileEditorFileStyleActive;
        private static GUIStyle fileEditorDirectoryStyle;
        private static GUIStyle fileEditorDirectoryStyleActive;
        private static GUIStyle fileEditorRenameFileField;
        private static GUIStyle fileEditorErrorText;
        private static GUIStyle fileEditorTextArea;

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

            labelStyleSettingName = new GUIStyle(labelStyle);
            labelStyleSettingName.wordWrap = true;
            labelStyleSettingName.clipping = TextClipping.Clip;

            labelStyleInfo = new GUIStyle(labelStyleSettingName);
            labelStyleInfo.hover.textColor = _fontColorValueChanged.Value;
            labelStyleInfo.hover.background = TooltipBackground;
            labelStyleInfo.alignment = TextAnchor.MiddleCenter;
            labelStyleInfo.margin.right = 10;
            labelStyleInfo.margin.top = 6;
            labelStyleInfo.padding = new RectOffset(0, 0, 0, 0);

            labelStyleValueDefault = new GUIStyle(labelStyle);
            labelStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;

            labelStyleValueChanged = new GUIStyle(labelStyle);
            labelStyleValueChanged.normal.textColor = _fontColorValueChanged.Value;

            textStyle = new GUIStyle(GUI.skin.textArea);
            textStyle.normal.textColor = _fontColor.Value;
            textStyle.fontSize = fontSize;

            textStyleValueDefault = new GUIStyle(textStyle);
            textStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;

            textStyleValueChanged = new GUIStyle(textStyle);
            textStyleValueChanged.normal.textColor = _fontColorValueChanged.Value;

            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = _fontColor.Value;
            buttonStyle.onNormal.textColor = _fontColor.Value;
            buttonStyle.fontSize = fontSize;

            buttonStyleValueDefault = new GUIStyle(buttonStyle);
            buttonStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;
            buttonStyleValueDefault.onNormal.textColor = _fontColorValueDefault.Value;

            buttonStyleValueChanged = new GUIStyle(buttonStyle);
            buttonStyleValueChanged.normal.textColor = _fontColorValueChanged.Value;
            buttonStyleValueChanged.onNormal.textColor = _fontColorValueChanged.Value;

            categoryHeaderStyleDefault = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                stretchWidth = true
            };
            categoryHeaderStyleDefault.padding.top = 1;
            categoryHeaderStyleDefault.padding.bottom = 1;

            categoryHeaderStyleChanged = new GUIStyle(categoryHeaderStyleDefault);
            categoryHeaderStyleChanged.normal.textColor = _fontColorValueChanged.Value;
            categoryHeaderStyleChanged.onNormal.textColor = _fontColorValueChanged.Value;

            pluginHeaderStyle = new GUIStyle(categoryHeaderStyleDefault);
            
            pluginHeaderStyleActive = new GUIStyle(pluginHeaderStyle);
            pluginHeaderStyleActive.normal.textColor = _fontColorValueChanged.Value;

            pluginHeaderStyleSplitView = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                stretchWidth = false
            };

            pluginHeaderStyleSplitViewActive = new GUIStyle(pluginHeaderStyleSplitView);
            pluginHeaderStyleSplitViewActive.normal.textColor = _fontColorValueChanged.Value;
            pluginHeaderStyleSplitViewActive.onNormal.textColor = _fontColorValueChanged.Value;

            toggleStyle = new GUIStyle(GUI.skin.toggle);
            toggleStyle.normal.textColor = _fontColor.Value;
            toggleStyle.onNormal.textColor = _fontColor.Value;
            toggleStyle.fontSize = fontSize;
            toggleStyle.imagePosition = ImagePosition.ImageLeft;
            toggleStyle.padding.top = 2;
            toggleStyle.padding.left = 16;
            toggleStyle.margin.top = 5;

            toggleStyleValueDefault = new GUIStyle(toggleStyle);
            toggleStyleValueDefault.normal.textColor = _fontColorValueDefault.Value;
            toggleStyleValueDefault.onNormal.textColor = _fontColorValueDefault.Value;

            toggleStyleValueChanged = new GUIStyle(toggleStyle);
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

            backgroundStyleWithHover = new GUIStyle(backgroundStyle);
            backgroundStyleWithHover.hover.background = TooltipBackground;

            categoryBackgroundStyle = new GUIStyle(backgroundStyle);
            categoryBackgroundStyle.margin.bottom = 6;
            categoryBackgroundStyle.margin.top = 0;

            categoryHeaderBackgroundStyle = new GUIStyle(backgroundStyle);
            categoryHeaderBackgroundStyle.normal.background = HeaderBackground;
            categoryHeaderBackgroundStyle.margin.bottom = 0;
            categoryHeaderBackgroundStyle.padding.bottom = 0;
            categoryHeaderBackgroundStyle.padding.top = 0;

            categoryHeaderBackgroundStyleWithHover = new GUIStyle(categoryHeaderBackgroundStyle);
            categoryHeaderBackgroundStyleWithHover.hover.background = HeaderBackgroundHover;

            tooltipStyle = new GUIStyle(GUI.skin.box);
            tooltipStyle.normal.textColor = _fontColor.Value;
            tooltipStyle.fontSize = fontSize;
            tooltipStyle.wordWrap = false;
            tooltipStyle.alignment = TextAnchor.MiddleLeft;
            tooltipStyle.normal.background = TooltipBackground;
            tooltipStyle.padding.left = 10;
            tooltipStyle.padding.right = 10;
            tooltipStyle.richText = true;

            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);

            thumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);

            fileEditorDirectoryStyle = new GUIStyle(toggleStyle);
            fileEditorDirectoryStyle.wordWrap = false;
            fileEditorDirectoryStyle.margin = new RectOffset(4, 4, 4, 4);
            fileEditorDirectoryStyle.fontStyle = FontStyle.Bold;

            fileEditorDirectoryStyleActive = new GUIStyle(fileEditorDirectoryStyle);
            fileEditorDirectoryStyleActive.normal.textColor = _fontColorValueChanged.Value;
            fileEditorDirectoryStyleActive.onNormal.textColor = _fontColorValueChanged.Value;

            fileEditorFileStyle = new GUIStyle(labelStyle);
            fileEditorFileStyle.padding.left = 6;
            fileEditorFileStyle.wordWrap = false;
            fileEditorFileStyle.margin.bottom = 2;
            fileEditorFileStyle.margin.top = 2;

            fileEditorFileStyleActive = new GUIStyle(fileEditorFileStyle);
            fileEditorFileStyleActive.normal.textColor = _fontColorValueChanged.Value;
            fileEditorFileStyleActive.onNormal.textColor = _fontColorValueChanged.Value;

            fileEditorRenameFileField = new GUIStyle(textStyleValueChanged);
            fileEditorRenameFileField.wordWrap = true;

            fileEditorErrorText = new GUIStyle(labelStyle);
            fileEditorErrorText.normal.textColor = Color.red;

            fileEditorTextArea = new GUIStyle(GUI.skin.textArea);
            fileEditorTextArea.padding = new RectOffset(5, 5, 5, 5);
            fileEditorTextArea.wordWrap = true;
            fileEditorTextArea.richText = true;
        }

        public static GUIStyle GetWindowStyle() => windowStyle;
        public static GUIStyle GetCategoryStyle(bool isDefaultStyle = true) => isDefaultStyle ? categoryHeaderStyleDefault : categoryHeaderStyleChanged;
        public static GUIStyle GetHeaderStyle(bool isActive) => isActive ? pluginHeaderStyleActive : pluginHeaderStyle;
        public static GUIStyle GetHeaderStyleSplitView(bool isActivePlugin = false) => isActivePlugin ? pluginHeaderStyleSplitViewActive : pluginHeaderStyleSplitView;
        public static GUIStyle GetSliderStyle() => sliderStyle;
        public static GUIStyle GetThumbStyle() => thumbStyle;
        public static GUIStyle GetBoxStyle() => boxStyle;
        public static GUIStyle GetTooltipStyle() => tooltipStyle;
        public static GUIStyle GetBackgroundStyle(bool withHover = false) => withHover ? backgroundStyleWithHover : backgroundStyle;
        public static GUIStyle GetCategoryBackgroundStyle() => categoryBackgroundStyle;
        public static GUIStyle GetCategoryHeaderBackgroundStyle(bool withHover = false) => withHover ? categoryHeaderBackgroundStyleWithHover : categoryHeaderBackgroundStyle;
        public static GUIStyle GetComboBoxStyle() => comboBoxStyle;
        public static GUIStyle GetToggleStyle() => toggleStyle;
        public static GUIStyle GetToggleStyle(bool isDefaultValue = true) => isDefaultValue ? toggleStyleValueDefault : toggleStyleValueChanged;
        public static GUIStyle GetToggleStyle(SettingEntryBase setting) => GetToggleStyle(IsDefaultValue(setting));
        public static GUIStyle GetLabelStyle() => labelStyle;
        public static GUIStyle GetLabelStyle(bool isDefaultValue = true) => isDefaultValue ? labelStyleValueDefault : labelStyleValueChanged;
        public static GUIStyle GetLabelStyle(SettingEntryBase setting) => GetLabelStyle(IsDefaultValue(setting));
        public static GUIStyle GetLabelStyleInfo() => labelStyleInfo;
        public static GUIStyle GetLabelStyleSettingName() => labelStyleSettingName;
        public static GUIStyle GetButtonStyle() => buttonStyle;
        public static GUIStyle GetButtonStyle(bool isDefaultValue = true) => isDefaultValue ? buttonStyleValueDefault : buttonStyleValueChanged;
        public static GUIStyle GetButtonStyle(SettingEntryBase setting) => GetButtonStyle(IsDefaultValue(setting));
        public static GUIStyle GetTextStyle(bool isDefaultValue = true) => isDefaultValue ? textStyleValueDefault : textStyleValueChanged;
        public static GUIStyle GetTextStyle(SettingEntryBase setting) => GetTextStyle(IsDefaultValue(setting));
        public static GUIStyle GetTextStyle(float setting, float defaultValue) => GetTextStyle(setting == defaultValue);
        public static GUIStyle GetTextStyle(Color setting, Color defaultValue) => GetTextStyle(IsEqualColorConfig(setting, defaultValue));
        public static GUIStyle GetFileStyle(bool isActive) => isActive ? fileEditorFileStyleActive : fileEditorFileStyle;
        public static GUIStyle GetDirectoryStyle(bool isActive) => isActive ? fileEditorDirectoryStyleActive : fileEditorDirectoryStyle;
        public static GUIStyle GetFileNameFieldStyle() => fileEditorRenameFileField;
        public static GUIStyle GetFileNameErrorStyle() => fileEditorErrorText;
        public static GUIStyle GetFileEditorTextArea() => fileEditorTextArea; 
        public static bool IsEqualColorConfig(Color setting, Color defaultValue) => ColorUtility.ToHtmlStringRGBA(setting) == ColorUtility.ToHtmlStringRGBA(defaultValue);
        internal static bool IsDefaultValue(SettingEntryBase setting)
        {
            if (setting?.DefaultValue == null || setting.Get() == null)
                return true;

            try
            {
                return setting.SettingType == typeof(Color) ? IsEqualColorConfig((Color)setting.Get(), (Color)setting.DefaultValue) : setting.Get().ToString().Equals(setting.DefaultValue.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }
    }
}
