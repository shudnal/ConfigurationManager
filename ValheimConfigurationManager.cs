using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ConditionalConfigSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YamlDotNet.Serialization;

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

        internal static string[] hiddenSettingsFileNames = new[] { $"{GUID}.hiddensettings.json", "shudnal.ConfigurationManager.hiddensettings.json" };

        public static ConfigEntry<bool> _configLocked;
        public static ConfigEntry<bool> _pauseGame;
        public static ConfigEntry<PreventInput> _preventInput;
        public static ConfigEntry<bool> _showMainMenuButton;
        public static ConfigEntry<string> _mainMenuButtonCaption;
        public static ConfigEntry<bool> _useValheimGuiScaleFactor;

        private static readonly Harmony harmony = new Harmony(GUID);

        internal static readonly ConfigSync configSync = new ConfigSync(GUID)
        {
            DisplayName = pluginName,
            CurrentVersion = Version,
            MinimumRequiredVersion = Version,
            ModRequired = false
        };

        internal static readonly CustomSyncedValue<List<string>> hiddenSettings = new CustomSyncedValue<List<string>>(configSync, "Hidden settings", new List<string>());

        private static DirectoryInfo pluginDirectory;
        private static DirectoryInfo configDirectory;
        private readonly List<FileSystemWatcher> _hiddenSettingsWatchers = new List<FileSystemWatcher>();
        private readonly HashSet<Button> _menuButtons = new HashSet<Button>();

        void OnEnable()
        {
            _configLocked = serverConfig("Valheim", "Lock Configuration", defaultValue: true, "Configuration is locked and can be changed by server admins only.");
            _pauseGame = config("Valheim", "Pause game", false, "Pause the game (if game can be paused) when window is open");
            _preventInput = config("Valheim", "Prevent input", PreventInput.Player, "Prevent input when window is open" +
                                                                                    "\n Off - everything goes through" +
                                                                                    "\n Player - prevent player controls and HUD buttons (console will still operate)" +
                                                                                    "\n All - also prevent console input");
            _showMainMenuButton = config("Valheim", "Main menu button", true, "Add button in main menu to open/close configuration manager window");
            _mainMenuButtonCaption = config("Valheim", "Main menu button caption", "Mods settings", "Main menu button caption");
            _useValheimGuiScaleFactor = config("Valheim", "Use Valheim GUI scaling", true, "Use Valheim scale factor from Accessibility - Scale GUI");

            _showMainMenuButton.SettingChanged += MenuButtonSettingChanged;
            _mainMenuButtonCaption.SettingChanged += MenuButtonSettingChanged;
            _preventInput.SettingChanged += InputPreventionSettingChanged;

            _ = configSync.AddLockingConfigEntry(_configLocked);

            harmony.PatchAll();

            DisplayingWindowChanged += ConfigurationManager_DisplayingWindowChanged;

            pluginDirectory = new DirectoryInfo(Assembly.GetExecutingAssembly().Location).Parent;
            configDirectory = new DirectoryInfo(Paths.ConfigPath);

            SetupHiddenSettingsWatcher();
            SetupMenuButton();
        }

        void OnDisable()
        {
            try
            {
                DisplayingWindow = false;
            }
            finally
            {
                ReleaseWindowCursor();
                ResetInputPrevention();
                harmony.UnpatchSelf();
                DisplayingWindowChanged -= ConfigurationManager_DisplayingWindowChanged;
                _showMainMenuButton.SettingChanged -= MenuButtonSettingChanged;
                _mainMenuButtonCaption.SettingChanged -= MenuButtonSettingChanged;
                _preventInput.SettingChanged -= InputPreventionSettingChanged;
                foreach (FileSystemWatcher watcher in _hiddenSettingsWatchers)
                    watcher.Dispose();
                _hiddenSettingsWatchers.Clear();
                foreach (Button button in _menuButtons)
                {
                    if (!button)
                        continue;
                    button.onClick.RemoveListener(ToggleWindow);
                    Navigation navigation = button.navigation;
                    if (navigation.selectOnUp)
                    {
                        Navigation previous = navigation.selectOnUp.navigation;
                        if (previous.selectOnDown == button)
                        {
                            previous.selectOnDown = navigation.selectOnDown;
                            navigation.selectOnUp.navigation = previous;
                        }
                    }
                    if (navigation.selectOnDown)
                    {
                        Navigation next = navigation.selectOnDown.navigation;
                        if (next.selectOnUp == button)
                        {
                            next.selectOnUp = navigation.selectOnUp;
                            navigation.selectOnDown.navigation = next;
                        }
                    }
                    button.gameObject.SetActive(false);
                }
            }
        }

        private void MenuButtonSettingChanged(object sender, EventArgs args) => SetupMenuButton();

        /// <summary>
        /// Toggle configuration manager window visibility
        /// </summary>
        public void ToggleWindow()
        {
            DisplayingWindow = !DisplayingWindow;
        }

        private void SetupHiddenSettingsWatcher()
        {
            foreach (string hiddenSettingsFileName in hiddenSettingsFileNames)
            {
                FileSystemWatcher fileSystemWatcherPlugin = new FileSystemWatcher(pluginDirectory.FullName, hiddenSettingsFileName);
                fileSystemWatcherPlugin.Changed += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.Created += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.Renamed += new RenamedEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.Deleted += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherPlugin.IncludeSubdirectories = true;
                fileSystemWatcherPlugin.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                _hiddenSettingsWatchers.Add(fileSystemWatcherPlugin);
                fileSystemWatcherPlugin.EnableRaisingEvents = true;

                FileSystemWatcher fileSystemWatcherConfig = new FileSystemWatcher(configDirectory.FullName, hiddenSettingsFileName);
                fileSystemWatcherConfig.Changed += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherConfig.Created += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherConfig.Renamed += new RenamedEventHandler(ReadConfigs);
                fileSystemWatcherConfig.Deleted += new FileSystemEventHandler(ReadConfigs);
                fileSystemWatcherConfig.IncludeSubdirectories = true;
                fileSystemWatcherConfig.SynchronizingObject = ThreadingHelper.SynchronizingObject;
                _hiddenSettingsWatchers.Add(fileSystemWatcherConfig);
                fileSystemWatcherConfig.EnableRaisingEvents = true;
            }

            ReadConfigs();
        }

        private static void ReadConfigs(object sender = null, FileSystemEventArgs eargs = null)
        {
            List<string> hiddenSettingsList = new List<string>();

            List<FileInfo> hiddenSettingsFiles = new List<FileInfo>();

            hiddenSettingsFileNames.Do(filename => hiddenSettingsFiles.AddRange(pluginDirectory.GetFiles(filename, SearchOption.AllDirectories)));
            hiddenSettingsFileNames.Do(filename => hiddenSettingsFiles.AddRange(configDirectory.GetFiles(filename, SearchOption.AllDirectories)));

            foreach (FileInfo file in hiddenSettingsFiles)
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

                        hiddenSettingsList.AddRange(new DeserializerBuilder().Build().Deserialize<List<string>>(text));
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

        private Menu _blockedMenu;
        private Menu.CloseMenuState _previousMenuCloseState;
        private Game _pausedGame;

        private void ConfigurationManager_DisplayingWindowChanged(object sender, ValueChangedEventArgs<bool> e)
        {
            if (DisplayingWindow)
            {
                if (Menu.instance)
                {
                    _blockedMenu = Menu.instance;
                    _previousMenuCloseState = _blockedMenu.m_closeMenuState;
                    _blockedMenu.m_closeMenuState = Menu.CloseMenuState.SettingsOpen;
                    _blockedMenu.m_rebuildLayout = true;
                }

                if (_pauseGame.Value && Game.instance && !Game.IsPaused() && Game.CanPause())
                {
                    _pausedGame = Game.instance;
                    Game.Pause();
                }
            }
            else
            {
                if (_blockedMenu && _blockedMenu.m_closeMenuState == Menu.CloseMenuState.SettingsOpen)
                {
                    _blockedMenu.m_closeMenuState = _previousMenuCloseState;
                    _blockedMenu.m_rebuildLayout = true;
                }
                _blockedMenu = null;

                if (_pausedGame && _pausedGame == Game.instance && !Menu.IsActive() && Game.IsPaused())
                    Game.Unpause();
                _pausedGame = null;
            }
        }

        private bool HideSettings()
        {
            return hiddenSettings.Value.Count > 0 && !configSync.IsAdmin;
        }

        private void SetupMenuButton()
        {
            if (FejdStartup.instance && FejdStartup.instance.m_menuList && FejdStartup.instance.m_menuButtons != null && FejdStartup.instance.m_menuButtons.Length > 0)
            {
                SetupMainMenuButton(FejdStartup.instance.m_menuList.transform.Find("MenuEntries"));
                FejdStartup.instance.m_menuButtons = FejdStartup.instance.m_menuList.GetComponentsInChildren<Button>();
            }

            if (Menu.instance)
                SetupMainMenuButton(Menu.instance.m_menuDialog?.Find("MenuEntries"));
        }

        private void SetupMainMenuButton(Transform menuEntries)
        {
            if (menuEntries == null)
                return;

            Transform settings = menuEntries.Find("Settings");
            if (!settings)
                return;

            GameObject menuButton = menuEntries.Find(menuButtonName)?.gameObject;
            if (menuButton == null)
            {
                menuButton = Instantiate(settings.gameObject, menuEntries);
                menuButton.transform.SetSiblingIndex(settings.GetSiblingIndex() + 1);
                menuButton.name = menuButtonName;

                Button button = menuButton.GetComponent<Button>();
                for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                    button.onClick.SetPersistentListenerState(index, UnityEngine.Events.UnityEventCallState.Off);
                var navigation = button.navigation;
                navigation.selectOnUp = settings.GetComponent<Button>();
                button.navigation = navigation;
            }

            Button modButton = menuButton.GetComponent<Button>();
            _menuButtons.RemoveWhere(button => !button);
            _menuButtons.Add(modButton);
            modButton.onClick.RemoveListener(ToggleWindow);
            modButton.onClick.AddListener(ToggleWindow);

            menuButton.GetComponentInChildren<TMP_Text>().text = _mainMenuButtonCaption.Value;
            menuButton.SetActive(_showMainMenuButton.Value);

            Button previousButton = settings.GetComponent<Button>();
            var previousNavigation = previousButton.navigation;

            Button nextButton = modButton.navigation.selectOnDown as Button;
            if (!nextButton)
                return;
            var nextNavigation = nextButton.navigation;

            if (_showMainMenuButton.Value)
            {
                previousNavigation.selectOnDown = modButton;
                nextNavigation.selectOnUp = modButton;
            }
            else
            {
                previousNavigation.selectOnDown = modButton.navigation.selectOnDown;
                nextNavigation.selectOnUp = modButton.navigation.selectOnUp;
            }

            previousButton.navigation = previousNavigation;
            nextButton.navigation = nextNavigation;
        }

        public float GetScreenSizeFactor()
        {
            float a = (float)ScreenSystemWidth / GuiScaler.m_minWidth;
            float b = (float)ScreenSystemHeight / GuiScaler.m_minHeight;
            return Mathf.Min(a, b) * GuiScaler.m_largeGuiScale;
        }

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
        public static class FejdStartup_Start_MenuButton
        {
            public static void Postfix() => instance.SetupMenuButton();
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.Start))]
        public static class Menu_Start_MenuButton
        {
            public static void Postfix() => instance.SetupMenuButton();
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.UpdateNavigation))]
        public static class Menu_UpdateNavigation_MenuButton
        {
            public static void Postfix() => instance.SetupMenuButton();
        }
    }
}
