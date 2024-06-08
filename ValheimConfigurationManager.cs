using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Configuration;
using BepInEx;
using ServerSync;
using System;
using System.IO;
using TMPro;

namespace ConfigurationManager
{
    public partial class ConfigurationManager
    {
        public enum PreventInput
        {
            Off,
            Player,
            All
        }

        internal const string menuButtonName = "Configuration Manager";

        internal static string hiddenSettingsFileName = $"{pluginID}.hiddensettings.json";

        public static ConfigEntry<bool> _pauseGame;
        public static ConfigEntry<PreventInput> _preventInput;
        public static ConfigEntry<bool> _showMainMenuButton;
        public static ConfigEntry<string> _mainMenuButtonCaption;

        private static readonly Harmony harmony = new Harmony(pluginID);

        internal static readonly ConfigSync configSync = new ConfigSync(pluginID) { DisplayName = pluginName, CurrentVersion = pluginVersion, MinimumRequiredVersion = pluginVersion };

        internal static readonly CustomSyncedValue<List<string>> hiddenSettings = new CustomSyncedValue<List<string>>(configSync, "Hidden settings", new List<string>());
        
        private static DirectoryInfo pluginDirectory;
        private static DirectoryInfo configDirectory;

        private static GameObject menuButton;

        void OnEnable()
        {
            _pauseGame = Config.Bind("Valheim", "Pause game", false, new ConfigDescription("Pause the game (if game can be paused) when window is open"));
            _preventInput = Config.Bind("Valheim", "Prevent input", PreventInput.Player, new ConfigDescription("Prevent input when window is open" +
                                                                                                                        "\n Off - everything goes through" +
                                                                                                                        "\n Player - prevent player controller (hotkeys like inventory, console and such will still operate)" +
                                                                                                                        "\n All - prevent all input events"));
            _showMainMenuButton = Config.Bind("Valheim", "Main menu button", true, new ConfigDescription("Add button in main menu to open/close configuration manager window"));
            _mainMenuButtonCaption = Config.Bind("Valheim", "Main menu button caption", "Mods settings", new ConfigDescription("Main menu button caption"));

            _showMainMenuButton.SettingChanged += (sender, args) => SetupMenuButton();
            _mainMenuButtonCaption.SettingChanged += (sender, args) => SetupMenuButton();

            harmony.PatchAll();

            DisplayingWindowChanged += ConfigurationManager_DisplayingWindowChanged;

            pluginDirectory = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent;
            configDirectory = new DirectoryInfo(Paths.ConfigPath);

            SetupHiddenSettingsWatcher();
        }

        void OnDisable()
        {
            harmony?.UnpatchSelf();
            DisplayingWindowChanged -= ConfigurationManager_DisplayingWindowChanged;
        }

        /// <summary>
        /// Toggle configuration manager window visibility
        /// </summary>
        public void ToggleWindow()
        {
            DisplayingWindow = !DisplayingWindow;
        }

        private static void SetupHiddenSettingsWatcher()
        {
            FileSystemWatcher fileSystemWatcherPlugin = new FileSystemWatcher(pluginDirectory.FullName, hiddenSettingsFileName);
            fileSystemWatcherPlugin.Changed += new FileSystemEventHandler(ReadConfigs);
            fileSystemWatcherPlugin.Created += new FileSystemEventHandler(ReadConfigs);
            fileSystemWatcherPlugin.Renamed += new RenamedEventHandler(ReadConfigs);
            fileSystemWatcherPlugin.Deleted += new FileSystemEventHandler(ReadConfigs);
            fileSystemWatcherPlugin.IncludeSubdirectories = true;
            fileSystemWatcherPlugin.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            fileSystemWatcherPlugin.EnableRaisingEvents = true;

            FileSystemWatcher fileSystemWatcherConfig = new FileSystemWatcher(configDirectory.FullName, hiddenSettingsFileName);
            fileSystemWatcherConfig.Changed += new FileSystemEventHandler(ReadConfigs);
            fileSystemWatcherConfig.Created += new FileSystemEventHandler(ReadConfigs);
            fileSystemWatcherConfig.Renamed += new RenamedEventHandler(ReadConfigs);
            fileSystemWatcherConfig.Deleted += new FileSystemEventHandler(ReadConfigs);
            fileSystemWatcherConfig.IncludeSubdirectories = true;
            fileSystemWatcherConfig.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            fileSystemWatcherConfig.EnableRaisingEvents = true;

            ReadConfigs();
        }

        private static void ReadConfigs(object sender = null, FileSystemEventArgs eargs = null)
        {
            List<string> hiddenSettingsList = new List<string>();

            foreach (FileInfo file in pluginDirectory.GetFiles(hiddenSettingsFileName, SearchOption.AllDirectories).AddRangeToArray(configDirectory.GetFiles(hiddenSettingsFileName, SearchOption.AllDirectories)))
            {
                LogInfo($"Loading {file.FullName}");

                try
                {
                    using (FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (StreamReader reader = new StreamReader(fs))
                    {
                        string text = reader.ReadToEnd();
                        if (text.IsNullOrWhiteSpace())
                            continue;

                        hiddenSettingsList.AddRange(LitJson.JsonMapper.ToObject<List<string>>(text));
                        reader.Close();
                        fs.Dispose();
                    }
                }
                catch (Exception e)
                {
                    LogInfo($"Error reading file ({file.FullName})! Error: {e.Message}");
                }
            }

            hiddenSettings.AssignLocalValue(hiddenSettingsList);
        }

        private static bool PreventAllInput()
        {
            return _preventInput.Value == PreventInput.All || (Game.IsPaused() && !GameCamera.InFreeFly());
        }

        private static bool PreventPlayerInput()
        {
            return PreventAllInput() || _preventInput.Value == PreventInput.Player;
        }

        private void ConfigurationManager_DisplayingWindowChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            if (FejdStartup.instance && FejdStartup.instance.m_mainMenu.activeSelf)
            {
                FejdStartup.instance?.m_mainMenu.SetActive(value: false);
                FejdStartup.instance?.m_mainMenu.SetActive(value: true);
            }

            if (Menu.instance)
            {
                Menu.instance.m_closeMenuState = DisplayingWindow ? Menu.CloseMenuState.SettingsOpen : Menu.CloseMenuState.CanBeClosed;
                Menu.instance.m_rebuildLayout = true;
            }

            if (!_pauseGame.Value || !Game.instance)
                return;

            if (DisplayingWindow && !Game.IsPaused() && Game.CanPause())
                Game.Pause();
            else if (!DisplayingWindow && !Menu.IsVisible() && Game.IsPaused())
                Game.Unpause();
        }

        private bool HideSettings()
        {
            return hiddenSettings.Value.Count > 0 && ZNet.instance != null && !ZNet.instance.LocalPlayerIsAdminOrHost();
        }

        private void SetupMenuButton()
        {
            if (FejdStartup.instance)
            {
                SetupMainMenuButton(FejdStartup.instance.m_menuList.transform.Find("MenuEntries"));
                FejdStartup.instance.m_menuButtons = FejdStartup.instance.m_menuList.GetComponentsInChildren<Button>();
            }


            if (Menu.instance)
                SetupMainMenuButton(Menu.instance.m_menuDialog.Find("MenuEntries"));
        }

        private void SetupMainMenuButton(Transform menuEntries)
        {
            menuButton = menuEntries.Find(menuButtonName)?.gameObject;
            if (menuButton == null)
            {
                Transform settings = menuEntries.Find("Settings");

                menuButton = Instantiate(settings.gameObject, menuEntries);
                menuButton.transform.SetSiblingIndex(settings.GetSiblingIndex() + 1);
                menuButton.name = menuButtonName;
                
                Button button = menuButton.GetComponent<Button>();
                button.onClick.SetPersistentListenerState(0, UnityEngine.Events.UnityEventCallState.Off);
                button.onClick.AddListener(delegate
                {
                    ToggleWindow();
                });
            }

            menuButton.GetComponentInChildren<TMP_Text>().text = _mainMenuButtonCaption.Value;
            menuButton.SetActive(_showMainMenuButton.Value);
        }

        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.TakeInput))]
        [HarmonyPriority(Priority.Last)]
        public static class PlayerController_TakeInput_PreventInput
        {
            public static void Postfix(ref bool __result)
            {
                if (PreventPlayerInput())
                    __result = __result && !instance.DisplayingWindow;
            }
        }

        [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
        [HarmonyPriority(Priority.Last)]
        public static class TextInput_IsVisible_PreventInput
        {
            public static void Postfix(ref bool __result)
            {
                if (PreventPlayerInput())
                    __result = __result || instance.DisplayingWindow;
            }
        }

        [HarmonyPatch]
        public static class ZInput_PreventAllInput
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.AcceptInputFromSource));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetKey));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetKeyDown));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetKeyNew));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseButton));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseButtonDown));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseButtonUp));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseButtonNew));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButton));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonDown));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonUp)); 
            }

            [HarmonyPriority(Priority.First)]
            private static bool Prefix(ref bool __result) => !PreventAllInput() || !instance.DisplayingWindow || (__result = false);
        }

        [HarmonyPatch]
        public static class ZInput_Float_PreventMouseInput
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetAxis));
                yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));
            }

            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ref float __result)
            {
                if (PreventPlayerInput() && instance.DisplayingWindow)
                    __result = 0f;
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseDelta))]
        public static class ZInput_GetMouseDelta_PreventMouseInput
        {
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(ref Vector2 __result)
            {
                if (PreventPlayerInput() && instance.DisplayingWindow)
                    __result = Vector2.zero;
            }
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
        public static class FejdStartup_Start_MenuButton
        {
            public static void Postfix()
            {
                instance.SetupMenuButton();
            }
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.Start))]
        public static class Menu_Start_MenuButton
        {
            public static void Postfix()
            {
                instance.SetupMenuButton();
            }
        }
    }
}
