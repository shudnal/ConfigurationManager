// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace ConfigurationManager
{
    internal sealed class ConfigSettingEntry : SettingEntryBase
    {
        public ConfigEntryBase Entry { get; }
        private readonly ConfigSynchronizationInfo synchronizationInfo;
        private readonly List<DynamicAttributeSource> dynamicAttributeSources = new List<DynamicAttributeSource>();

        public ConfigSettingEntry(ConfigEntryBase entry, BaseUnityPlugin owner)
        {
            Entry = entry;

            DispName = entry.Definition.Key;
            Category = entry.Definition.Section;
            Description = entry.Description?.Description;

            var converter = TomlTypeConverter.GetConverter(entry.SettingType);
            if (converter != null)
            {
                ObjToStr = o => converter.ConvertToString(o, entry.SettingType);
                StrToObj = s => converter.ConvertToObject(s, entry.SettingType);
            }

            var values = entry.Description?.AcceptableValues;
            if (values != null)
                GetAcceptableValues(values);

            DefaultValue = entry.DefaultValue;

            SetFromAttributes(entry.Description?.Tags, owner);
            InitializeDynamicAttributeSources(entry.Description?.Tags);
            synchronizationInfo = ConfigSynchronizationInfo.Create(entry);
        }

        private void GetAcceptableValues(AcceptableValueBase values)
        {
            var t = values.GetType();
            var listProp = t.GetProperty(nameof(AcceptableValueList<bool>.AcceptableValues), BindingFlags.Instance | BindingFlags.Public);
            if (listProp != null)
            {
                AcceptableValues = ((IEnumerable)listProp.GetValue(values, null)).Cast<object>().ToArray();
            }
            else
            {
                var minProp = t.GetProperty(nameof(AcceptableValueRange<bool>.MinValue), BindingFlags.Instance | BindingFlags.Public);
                var maxProp = t.GetProperty(nameof(AcceptableValueRange<bool>.MaxValue), BindingFlags.Instance | BindingFlags.Public);
                if (minProp != null && maxProp != null)
                    AcceptableValueRange = new KeyValuePair<object, object>(minProp.GetValue(values, null), maxProp.GetValue(values, null));
            }
        }


        private void InitializeDynamicAttributeSources(object[] tags)
        {
            if (tags == null)
                return;

            foreach (object tag in tags)
            {
                if (tag == null)
                    continue;

                Type tagType = tag.GetType();
                if (tagType.Name != "ConfigurationManagerAttributes")
                    continue;

                PropertyInfo readOnlyProperty = tagType.GetProperty(nameof(ReadOnly), BindingFlags.Instance | BindingFlags.Public);
                FieldInfo readOnlyField = tagType.GetField(nameof(ReadOnly), BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo browsableProperty = tagType.GetProperty(nameof(Browsable), BindingFlags.Instance | BindingFlags.Public);
                FieldInfo browsableField = tagType.GetField(nameof(Browsable), BindingFlags.Instance | BindingFlags.Public);

                if (readOnlyProperty != null || readOnlyField != null || browsableProperty != null || browsableField != null)
                {
                    dynamicAttributeSources.Add(new DynamicAttributeSource(
                        tag,
                        tagType,
                        readOnlyProperty,
                        readOnlyField,
                        browsableProperty,
                        browsableField));
                }
            }
        }

        internal override bool RefreshDynamicAttributes()
        {
            bool? previousReadOnly = ReadOnly;
            bool? previousBrowsable = Browsable;

            foreach (DynamicAttributeSource source in dynamicAttributeSources)
            {
                try
                {
                    if (source.TryGetReadOnly(out bool readOnly))
                        ReadOnly = readOnly;
                    if (source.TryGetBrowsable(out bool browsable))
                        Browsable = browsable;
                }
                catch (Exception ex)
                {
                    ConfigurationManager.LogInfo($"Failed to refresh dynamic attributes from {source.SourceType.FullName} - {ex.Message}");
                }
            }

            return previousReadOnly != ReadOnly || previousBrowsable != Browsable;
        }

        private sealed class DynamicAttributeSource
        {
            private readonly object source;
            private readonly PropertyInfo readOnlyProperty;
            private readonly FieldInfo readOnlyField;
            private readonly PropertyInfo browsableProperty;
            private readonly FieldInfo browsableField;

            internal DynamicAttributeSource(
                object source,
                Type sourceType,
                PropertyInfo readOnlyProperty,
                FieldInfo readOnlyField,
                PropertyInfo browsableProperty,
                FieldInfo browsableField)
            {
                this.source = source;
                SourceType = sourceType;
                this.readOnlyProperty = readOnlyProperty;
                this.readOnlyField = readOnlyField;
                this.browsableProperty = browsableProperty;
                this.browsableField = browsableField;
            }

            internal Type SourceType { get; }

            internal bool TryGetReadOnly(out bool value)
            {
                return TryGetBoolean(readOnlyProperty, readOnlyField, out value);
            }

            internal bool TryGetBrowsable(out bool value)
            {
                return TryGetBoolean(browsableProperty, browsableField, out value);
            }

            private bool TryGetBoolean(PropertyInfo property, FieldInfo field, out bool value)
            {
                object memberValue = property != null ? property.GetValue(source, null) : field?.GetValue(source);
                if (memberValue is bool booleanValue)
                {
                    value = booleanValue;
                    return true;
                }

                value = false;
                return false;
            }
        }

        public override Type SettingType => Entry.SettingType;

        public override object Get()
        {
            return Entry.BoxedValue;
        }

        protected override void SetValue(object newVal)
        {
            Entry.BoxedValue = newVal;
        }

        internal ConfigSynchronizationState GetSynchronizationState()
        {
            return synchronizationInfo.GetState();
        }

        internal bool ToggleSynchronizationPolicy()
        {
            return synchronizationInfo.TogglePolicy();
        }

        internal bool ShouldBeHidden()
        {
            return ConfigurationManager.hiddenSettings.Value.Contains($"{PluginInfo.GUID}={Entry.Definition.Section}={Entry.Definition.Key}");
        }
    }
}
