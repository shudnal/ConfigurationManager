﻿// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;
using BepInEx;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;

namespace ConfigurationManager
{
    internal static class SettingSearcher
    {
        /// <summary>
        /// Search for all instances of BaseUnityPlugin loaded by chainloader or other means.
        /// </summary>
        public static BaseUnityPlugin[] FindPlugins()
        {
            // Search for instances of BaseUnityPlugin to also find dynamically loaded plugins.
            // Have to use FindObjectsOfType(Type) instead of FindObjectsOfType<T> because the latter is not available in some older unity versions.
            // Still look inside Chainloader.PluginInfos in case the BepInEx_Manager GameObject uses HideFlags.HideAndDontSave, which hides it from Object.Find methods.
            return Chainloader.PluginInfos.Values.Select(x => x.Instance)
                              .Where(plugin => plugin != null)
                              .Union(UnityEngine.Object.FindObjectsOfType(typeof(BaseUnityPlugin)).Cast<BaseUnityPlugin>())
                              .ToArray();
        }

        public static void CollectSettings(out IEnumerable<SettingEntryBase> results, out List<string> modsWithoutSettings)
        {
            modsWithoutSettings = new List<string>();

            try
            {
                results = GetBepInExCoreConfig();
            }
            catch (Exception ex)
            {
                results = Enumerable.Empty<SettingEntryBase>();
                ConfigurationManager.LogError(ex);
            }

            foreach (var plugin in FindPlugins())
            {
                var type = plugin.GetType();

                var pluginInfo = plugin.Info.Metadata;
                var pluginName = pluginInfo?.Name ?? plugin.GetType().FullName;

                if (type.GetCustomAttributes(typeof(BrowsableAttribute), false).Cast<BrowsableAttribute>()
                        .Any(x => !x.Browsable))
                {
                    modsWithoutSettings.Add(pluginName);
                    continue;
                }

                var detected = new List<SettingEntryBase>();

                detected.AddRange(GetPluginConfig(plugin));

                detected.RemoveAll(x => x.Browsable == false);

                if (detected.Count == 0)
                    modsWithoutSettings.Add(pluginName);

                if (detected.Count > 0)
                    results = results.Concat(detected);
            }
        }

        /// <summary>
        /// Get entries for all core BepInEx settings
        /// </summary>
        private static IEnumerable<SettingEntryBase> GetBepInExCoreConfig()
        {
            var coreConfigProp = typeof(ConfigFile).GetProperty("CoreConfig", BindingFlags.Static | BindingFlags.NonPublic);
            if (coreConfigProp == null) throw new ArgumentNullException(nameof(coreConfigProp));

            var coreConfig = (ConfigFile)coreConfigProp.GetValue(null, null);
            var bepinMeta = new BepInPlugin("BepInEx", "BepInEx", typeof(Chainloader).Assembly.GetName().Version.ToString());

            return coreConfig.Select(kvp => (SettingEntryBase)new ConfigSettingEntry(kvp.Value, null) { IsAdvanced = true, PluginInfo = bepinMeta });
        }

        /// <summary>
        /// Get entries for all settings of a plugin
        /// </summary>
        private static IEnumerable<ConfigSettingEntry> GetPluginConfig(BaseUnityPlugin plugin)
        {
            return plugin.Config.Select(kvp => new ConfigSettingEntry(kvp.Value, plugin));
        }
    }
}