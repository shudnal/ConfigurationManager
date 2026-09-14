// Based on code made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;
using BepInEx;
using BepInEx.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace ConfigurationManager
{
    internal static class SettingSearcher
    {
        /// <summary>
        /// Search for all fully initialized instances of BaseUnityPlugin loaded by chainloader or other means.
        /// </summary>
        public static BaseUnityPlugin[] FindPlugins()
        {
            // Search for instances of BaseUnityPlugin to also find dynamically loaded plugins.
            // Still look inside Chainloader.PluginInfos in case the BepInEx_Manager GameObject uses
            // HideFlags.HideAndDontSave, which hides it from Object.Find methods.
            IEnumerable<BaseUnityPlugin> chainloaderPlugins = Chainloader.PluginInfos.Values
                .Select(info => info?.Instance)
                .Where(IsInitializedPlugin);

            IEnumerable<BaseUnityPlugin> discoveredPlugins = UnityEngine.Object
                .FindObjectsByType(typeof(BaseUnityPlugin), FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Cast<BaseUnityPlugin>()
                .Where(IsInitializedPlugin);

            return chainloaderPlugins.Union(discoveredPlugins).ToArray();
        }

        /// <summary>
        /// BepInEx assigns PluginInfo.Instance only after AddComponent (including Awake) finishes successfully.
        /// A component left behind by a failed Awake therefore exists in Unity but is not a usable loaded plugin.
        /// Dynamically created plugins that did not go through Chainloader create their own PluginInfo with
        /// Instance already pointing at themselves, so they remain discoverable here.
        /// </summary>
        private static bool IsInitializedPlugin(BaseUnityPlugin plugin)
        {
            if (plugin == null)
                return false;

            try
            {
                PluginInfo info = plugin.Info;
                return info?.Metadata != null && ReferenceEquals(info.Instance, plugin);
            }
            catch
            {
                return false;
            }
        }

        public static void CollectSettings(out IEnumerable<SettingEntryBase> results, out List<string> modsWithoutSettings)
        {
            modsWithoutSettings = new List<string>();
            var collectedSettings = new List<SettingEntryBase>();

            try
            {
                // Materialize here so failures while constructing core setting entries are caught here,
                // instead of being deferred until BuildSettingList enumerates the result later.
                collectedSettings.AddRange(GetBepInExCoreConfig());
            }
            catch (Exception ex)
            {
                ConfigurationManager.LogError("Failed to collect BepInEx core settings.");
                ConfigurationManager.LogError(ex);
            }

            foreach (BaseUnityPlugin plugin in FindPlugins())
            {
                try
                {
                    Type type = plugin.GetType();
                    BepInPlugin pluginInfo = plugin.Info?.Metadata;
                    string pluginName = pluginInfo?.Name ?? type.FullName ?? "Unknown plugin";

                    if (pluginInfo == null)
                    {
                        ConfigurationManager.LogWarning($"Skipping plugin without metadata while collecting settings: {pluginName}");
                        continue;
                    }

                    if (type.GetCustomAttributes(typeof(BrowsableAttribute), false).Cast<BrowsableAttribute>()
                            .Any(attribute => !attribute.Browsable))
                    {
                        modsWithoutSettings.Add(pluginName);
                        continue;
                    }

                    var detected = new List<SettingEntryBase>();
                    detected.AddRange(GetPluginConfig(plugin));
                    detected.RemoveAll(setting => setting == null || setting.Browsable == false || setting.PluginInfo == null);

                    if (detected.Count == 0)
                        modsWithoutSettings.Add(pluginName);
                    else
                        collectedSettings.AddRange(detected);
                }
                catch (Exception ex)
                {
                    // One broken or partially initialized plugin must never prevent the manager from opening
                    // or hide settings belonging to every other plugin.
                    string pluginName = GetPluginNameSafe(plugin);
                    ConfigurationManager.LogError($"Failed to collect settings of plugin: {pluginName}. The plugin will be skipped.");
                    ConfigurationManager.LogError(ex);
                }
            }

            results = collectedSettings;
        }

        private static string GetPluginNameSafe(BaseUnityPlugin plugin)
        {
            if (plugin == null)
                return "Unknown plugin";

            try
            {
                return plugin.Info?.Metadata?.Name ?? plugin.GetType().FullName ?? "Unknown plugin";
            }
            catch
            {
                try
                {
                    return plugin.GetType().FullName ?? "Unknown plugin";
                }
                catch
                {
                    return "Unknown plugin";
                }
            }
        }

        /// <summary>
        /// Get entries for all core BepInEx settings
        /// </summary>
        private static IEnumerable<SettingEntryBase> GetBepInExCoreConfig()
        {
            PropertyInfo coreConfigProp = typeof(ConfigFile).GetProperty("CoreConfig", BindingFlags.Static | BindingFlags.NonPublic);
            if (coreConfigProp == null)
                throw new ArgumentNullException(nameof(coreConfigProp));

            var coreConfig = (ConfigFile)coreConfigProp.GetValue(null, null);
            if (coreConfig == null)
                throw new InvalidOperationException("BepInEx core config is not available.");

            var bepinMeta = new BepInPlugin("BepInEx", "BepInEx", typeof(Chainloader).Assembly.GetName().Version.ToString());

            return coreConfig.Select(kvp => (SettingEntryBase)new ConfigSettingEntry(kvp.Value, null)
            {
                IsAdvanced = true,
                PluginInfo = bepinMeta
            });
        }

        /// <summary>
        /// Get entries for all settings of a plugin
        /// </summary>
        private static IEnumerable<ConfigSettingEntry> GetPluginConfig(BaseUnityPlugin plugin)
        {
            if (plugin.Config == null)
                return Enumerable.Empty<ConfigSettingEntry>();

            return plugin.Config.Select(kvp => new ConfigSettingEntry(kvp.Value, plugin));
        }
    }
}
