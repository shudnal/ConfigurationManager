using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static ConfigurationManager.ConfigurationManagerStyles;
using static ConfigurationManager.ConfigurationManager;
using BepInEx;
using UtfUnknown;
using System;
using System.Linq;

namespace ConfigurationManager
{
    public class ConfigFilesEditor
    {
        private readonly string[] directories = new string[2] { Paths.ConfigPath, Paths.PluginPath };

        private readonly Dictionary<string, bool> folderStates = new Dictionary<string, bool>();

        private Vector2 scrollPosition;
        private string fileContent;
        private Vector2 textScrollPosition;

        private Rect windowRect = new Rect(100, 100, 800, 600);
        private bool _isOpen;

        private const int WindowId = -680;

        private const string SearchBoxName = "searchBoxEditor";
        private bool _focusSearchBox;
        private string _searchString;
        private string _errorText;
        private string _activeFile;
        private string _activeDirectory;

        public bool IsSearching => SearchString.Length > 1;

        public string SearchString
        {
            get => _searchString;
            private set
            {
                if (value == null)
                    value = string.Empty;

                if (_searchString == value)
                    return;

                _searchString = value;
            }
        }

        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen == value)
                    return;

                _isOpen = value;
            }
        }

        public ConfigFilesEditor()
        {
            windowRect = new Rect(_windowPositionTextEditor.Value, _windowSizeTextEditor.Value);
        }

        public void OnGUI()
        {
            if (!IsOpen)
                return;

            windowRect.size = _windowSizeTextEditor.Value;
            windowRect.position = _windowPositionTextEditor.Value;

            Color color = GUI.backgroundColor;
            GUI.backgroundColor = _windowBackgroundColor.Value;

            windowRect = GUI.Window(WindowId, windowRect, DrawWindow, _windowTitleTextEditor.Value, GetWindowStyle());

            if (!UnityInput.Current.GetKeyDown(KeyCode.Mouse0) && (windowRect.x != _windowPositionTextEditor.Value.x || windowRect.y != _windowPositionTextEditor.Value.y))
                SaveCurrentSizeAndPosition();
            
            GUI.backgroundColor = color;
        }

        internal void SaveCurrentSizeAndPosition()
        {
            _windowSizeTextEditor.Value = new Vector2(Mathf.Clamp(windowRect.size.x, 500f, instance.ScreenWidth), Mathf.Clamp(windowRect.size.y, 200f, instance.ScreenHeight));
            _windowPositionTextEditor.Value = new Vector2(Mathf.Clamp(windowRect.position.x, 0f, instance.ScreenWidth - _windowSize.Value.x / 4f), Mathf.Clamp(windowRect.position.y, 0f, instance.ScreenHeight - _headerSize * 2));
            instance.Config.Save();
        }

        private void DrawSearchBox()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(_searchTextEditor.Value, GetLabelStyle(), GUILayout.Width(GetLabelStyle().CalcSize(new GUIContent(_searchTextEditor.Value)).x + 4));

                GUI.SetNextControlName(SearchBoxName);
                SearchString = GUILayout.TextField(SearchString, GetTextStyle(), GUILayout.ExpandWidth(true));

                if (_focusSearchBox)
                {
                    GUI.FocusWindow(WindowId);
                    GUI.FocusControl(SearchBoxName);
                    _focusSearchBox = false;
                }
                Color color = GUI.backgroundColor;
                GUI.backgroundColor = _widgetBackgroundColor.Value;
                if (GUILayout.Button(_clearText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    SearchString = string.Empty;
                GUI.backgroundColor = color;
            }
            GUILayout.EndHorizontal();
        }
        
        private void DrawContentButtons()
        {
            GUILayout.BeginHorizontal();
            {
                try
                {
                    if (!_activeFile.IsNullOrWhiteSpace() && fileContent != File.ReadAllText(_activeFile))
                        if (GUILayout.Button(_saveFileTextEditor.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                            File.WriteAllText(_activeFile, fileContent, CharsetDetector.DetectFromFile(_activeFile).Detected.Encoding);
                }
                catch (Exception e)
                {
                    _errorText = e.Message;
                }

                GUILayout.Label(_errorText, GUILayout.ExpandWidth(true));

                if (GUILayout.Button(_closeText.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    IsOpen = false;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginHorizontal();
            {
                var backgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = _entryBackgroundColor.Value;

                // Tree
                GUILayout.BeginVertical(GetBackgroundStyle(), GUILayout.MaxWidth(windowRect.width * 0.3f));
                {
                    DrawSearchBox();

                    scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Width(windowRect.width * 0.3f));
                    DrawDirectories(directories);
                    GUILayout.EndScrollView();
                    DrawDirectoriesMenu();
                }
                GUILayout.EndVertical();

                // Content
                GUILayout.BeginVertical(GetBackgroundStyle(), GUILayout.MaxWidth(windowRect.width * 0.7f));
                {
                    DrawContentButtons();

                    textScrollPosition = GUILayout.BeginScrollView(textScrollPosition);
                    fileContent = GUILayout.TextArea(fileContent, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                    GUILayout.EndScrollView();
                }
                GUILayout.EndVertical();

                GUI.backgroundColor = backgroundColor;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));

            windowRect = Utilities.Utils.ResizeWindow(windowID, windowRect, out bool sizeChanged);

            if (sizeChanged)
                SaveCurrentSizeAndPosition();
        }

        private void DrawDirectory(string path)
        {
            DrawDirectories(Directory.GetDirectories(path));

            DrawFiles(path);
        }

        private void DrawDirectories(string[] directories)
        {
            foreach (string directory in directories)
            {
                if (!_showEmptyFolders.Value && !DirectoryContainsValidFiles(directory))
                    continue;

                string folderName = Path.GetFileName(directory);

                if (!folderStates.ContainsKey(directory))
                    folderStates[directory] = false;
                
                if (folderStates[directory] != (folderStates[directory] = GUILayout.Toggle(folderStates[directory], " " + folderName, GetToggleStyle(directory != _activeDirectory))) && folderStates[directory])
                    _activeDirectory = directory;

                if (folderStates[directory])
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    
                    GUILayout.BeginVertical();
                    DrawDirectory(directory); 
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawFiles(string path)
        {
            string[] files = Directory.GetFiles(path);

            foreach (string file in files)
                if (IsValidFile(file))
                    if (GUILayout.Button("  " + Path.GetFileName(file), GetLabelStyle(file != _activeFile), GUILayout.ExpandHeight(false)))
                        LoadFileToEditor(file); 
        }

        private bool IsValidFile(string file)
        {
            string filename = Path.GetFileName(file);
            if (filename == "manifest.json")
                return false;

            string extension = Path.GetExtension(file).ToLower();
            foreach (string validExtension in _editableExtensions.Value.Split(',').Select(GetNormalizedExtention))
                if (extension == validExtension)
                    return SearchString.IsNullOrWhiteSpace() || filename.IndexOf(SearchString, System.StringComparison.OrdinalIgnoreCase) > -1;

            return false;
        }

        private string GetNormalizedExtention(string extension)
        {
            if (extension.IsNullOrWhiteSpace())
                return string.Empty;

            string ext = extension.Trim().ToLower();
            return ext.StartsWith('.') ? ext : "." + ext;
        }

        private bool DirectoryContainsValidFiles(string path)
        {
            foreach (string file in Directory.GetFiles(path))
            {
                if (IsValidFile(file)) return true;
            }

            foreach (string directory in Directory.GetDirectories(path))
            {
                if (DirectoryContainsValidFiles(directory)) return true;
            }

            return false; 
        }

        private void LoadFileToEditor(string filePath)
        {
            try
            {
                fileContent = File.ReadAllText(filePath);
                _activeFile = filePath;
                _activeDirectory = Path.GetDirectoryName(filePath);
            }
            catch (IOException e)
            {
                LogError($"Failed to load file {filePath}: {e.Message}");
                _errorText = "Failed to load file";
                fileContent = e.Message;
            }
        }

        private string newItemName = "";
        private bool creatingFolder = false;
        private bool creatingFile = false;

        private void DrawDirectoriesMenu()
        {
            if (creatingFolder || creatingFile)
            {
                GUILayout.BeginHorizontal();
                newItemName = GUILayout.TextField(newItemName, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("OK", GetButtonStyle(), GUILayout.ExpandWidth(false)))
                {
                    CreateNewItem(newItemName, creatingFolder);
                    creatingFolder = false;
                    creatingFile = false;
                }
                if (GUILayout.Button("Cancel", GetButtonStyle(), GUILayout.ExpandWidth(false)))
                {
                    creatingFolder = false;
                    creatingFile = false;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New folder", GetButtonStyle()))
            {
                creatingFolder = true;
                creatingFile = false;
                newItemName = "";
            }
            if (GUILayout.Button("New file", GetButtonStyle()))
            {
                creatingFile = true;
                creatingFolder = false;
                newItemName = "";
            }
            
            _showEmptyFolders.Value = GUILayout.Toggle(_showEmptyFolders.Value, "Empty folders", GetToggleStyle(), GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();
        }

        private void CreateNewItem(string itemName, bool isFolder)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return;

            if (string.IsNullOrEmpty(_activeDirectory))
                return;

            string newPath = Path.Combine(_activeDirectory, itemName);

            if (isFolder)
            {
                Directory.CreateDirectory(newPath);
                _showEmptyFolders.Value = true;
                _activeDirectory = newPath;
            }
            else
            {
                File.Create(newPath).Close();
                _activeFile = newPath;
            }
        }
    }
}

