using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static ConfigurationManager.ConfigurationManagerStyles;
using static ConfigurationManager.ConfigurationManager;
using BepInEx;
using System;
using System.Linq;
using System.Diagnostics;
using BepInEx.Bootstrap;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using Newtonsoft.Json.Linq;

namespace ConfigurationManager
{
    public class ConfigFilesEditor
    {
        private enum FileEditState
        {
            None,
            CreatingFolder,
            CreatingFile,
            RenamingFile
        }

        private readonly string[] _directories = { Paths.ConfigPath, Paths.PluginPath };

        private readonly Dictionary<string, bool> _folderStates = new Dictionary<string, bool>();
        private readonly string _trashBinDirectory = Path.Combine(Paths.CachePath, "ConfigurationManagerTrashBin");

        private Vector2 _scrollPosition;
        private string _fileContent;
        private Vector2 _textScrollPosition;

        private Rect _windowRect = new Rect(_windowPositionTextEditor.Value, _windowSizeTextEditor.Value);

        private const int WindowId = -680;

        private const string SearchBoxName = "searchBoxEditor";
        private const int DirectoryOffset = 20;
        private bool _focusSearchBox;
        private bool _focusTextArea;
        private string _searchString;
        private string _errorText;
        private string _activeFile;
        private string _activeDirectory;
        private string _newItemName;
        private string _newItemErrorText;

        private FileEditState _fileNameState;

        private void SetFileEditState(FileEditState newState)
        {
            _fileNameState = newState;
            _newItemErrorText = string.Empty;
            _newItemName = string.Empty;
        }

        private string SearchString
        {
            get => _searchString;
            set
            {
                if (value == null)
                    value = string.Empty;

                _searchString = value;
            }
        }

        public bool IsOpen { get; set; }

        public void OnGUI()
        {
            if (!IsOpen)
                return;

            _windowRect.size = _windowSizeTextEditor.Value;
            _windowRect.position = _windowPositionTextEditor.Value;

            Color color = GUI.backgroundColor;
            GUI.backgroundColor = _windowBackgroundColor.Value;

            _windowRect = GUI.Window(WindowId, _windowRect, DrawWindow,
                _activeFile.IsNullOrWhiteSpace()
                    ? _windowTitleTextEditor.Value
                    : "..." + _activeFile.Replace(Path.GetDirectoryName(Paths.BepInExRootPath) ?? string.Empty, ""), GetWindowStyle());

            if (!UnityInput.Current.GetKeyDown(KeyCode.Mouse0) &&
                (_windowRect.position != _windowPositionTextEditor.Value))
                SaveCurrentSizeAndPosition();

            GUI.backgroundColor = color;
        }

        internal void SaveCurrentSizeAndPosition()
        {
            _windowSizeTextEditor.Value = new Vector2(Mathf.Clamp(_windowRect.size.x, 1000f / instance.ScaleFactor, instance.ScreenWidth), Mathf.Clamp(_windowRect.size.y, 600f / instance.ScaleFactor, instance.ScreenHeight));
            _windowPositionTextEditor.Value = new Vector2(Mathf.Clamp(_windowRect.position.x, 0f, instance.ScreenWidth - _windowSize.Value.x / 4f), Mathf.Clamp(_windowRect.position.y, 0f, instance.ScreenHeight - HeaderSize * 2));
            instance.Config.Save();
        }

        private void DrawFilters()
        {
            GUILayout.BeginHorizontal();
            {
                string label = "Files:";
                GUILayout.Label(label, GetLabelStyle(), GUILayout.Width(GetLabelStyle().CalcSize(new GUIContent(label)).x + 2));
                _editableExtensions.Value = GUILayout.TextField(_editableExtensions.Value, GetTextStyle(), GUILayout.ExpandWidth(true));

                Color color = GUI.backgroundColor;
                if (_hideModConfigs.Value)
                    GUI.backgroundColor = _enabledBackgroundColor.Value;

                _hideModConfigs.Value = GUILayout.Toggle(_hideModConfigs.Value, new GUIContent(_hideModConfigs.Definition.Key, _hideModConfigs.Description.Description), GetToggleStyle(), GUILayout.ExpandWidth(false));
                GUI.backgroundColor = color;
            }
            GUILayout.EndHorizontal();
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
                bool fileIsActive = !_activeFile.IsNullOrWhiteSpace();
                try
                {
                    GUI.enabled = fileIsActive && _fileContent != File.ReadAllText(_activeFile);
                    
                    if (GUILayout.Button(_saveFileTextEditor.Value, GetButtonStyle(), GUILayout.ExpandWidth(false)))
                        File.WriteAllText(_activeFile, _fileContent);
                }
                catch (Exception e)
                {
                    _errorText = e.Message;
                }
                finally
                {
                    GUI.enabled = true;
                }

                GUI.enabled = fileIsActive;
                
                if (GUILayout.Button("Validate JSON", GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _errorText = IsValidJSON(_fileContent);
                if (GUILayout.Button("Validate YAML", GetButtonStyle(), GUILayout.ExpandWidth(false)))
                    _errorText = IsValidYAML(_fileContent);

                GUI.enabled = true;

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
                GUILayout.BeginVertical(GetBackgroundStyle(), GUILayout.MaxWidth(GetFileListWidth()));
                {
                    DrawFilters();

                    DrawSearchBox();

                    _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(_windowRect.width * 0.3f));
                    _directoryDepth = 0;
                    DrawDirectories(_directories);
                    GUILayout.EndScrollView();
                    DrawDirectoriesMenu();
                }
                GUILayout.EndVertical();

                // Content
                GUILayout.BeginVertical(GetBackgroundStyle(), GUILayout.MaxWidth(_windowRect.width * 0.7f));
                {
                    DrawContentButtons();

                    _textScrollPosition = GUILayout.BeginScrollView(_textScrollPosition);

                    GUI.enabled = File.Exists(_activeFile);
                    
                    GUI.SetNextControlName("textEditor");
                    _fileContent = GUILayout.TextArea(_fileContent, GetFileEditorTextArea(), GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                    
                    if (_focusTextArea || GUI.GetNameOfFocusedControl() == "textEditor")
                    {
                        GUI.FocusWindow(WindowId);
                        GUI.FocusControl("textEditor");
                        _focusTextArea = false;
                    }

                    GUI.enabled = true;
                    GUILayout.EndScrollView();
                }
                GUILayout.EndVertical();

                GUI.backgroundColor = backgroundColor;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, _windowRect.width, HeaderSize));

            if (!SettingFieldDrawer.DrawCurrentDropdown())
                DrawTooltip(_windowRect);

            _windowRect = Utilities.Utils.ResizeWindow(windowID, _windowRect, out bool sizeChanged);

            if (sizeChanged)
                SaveCurrentSizeAndPosition();
        }

        private float GetFileListWidth() => _windowRect.width * 0.3f;

        private void DrawDirectory(string path)
        {
            _directoryDepth++;

            DrawDirectories(Directory.GetDirectories(path));

            if (path == _activeDirectory && _fileNameState == FileEditState.CreatingFolder)
                DrawFileNameField();

            DrawFiles(path);

            _directoryDepth--;
        }

        private int _directoryDepth;

        private void DrawDirectories(string[] directories)
        {
            foreach (string directory in directories)
            {
                if (!_showEmptyFolders.Value && !DirectoryContainsValidFiles(directory))
                    continue;

                if (!_folderStates.ContainsKey(directory))
                    _folderStates[directory] = false;

                if (_folderStates[directory] != (_folderStates[directory] = GUILayout.Toggle(_folderStates[directory], Path.GetFileName(directory), GetDirectoryStyle(directory == _activeDirectory))) && _folderStates[directory])
                {
                    _activeDirectory = directory;
                    SetFileEditState(FileEditState.None);
                }

                if (_folderStates[directory])
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(DirectoryOffset);
                    
                    GUILayout.BeginVertical();
                    DrawDirectory(directory); 
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawFileNameField()
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal(GUILayout.MaxWidth(GetFileListWidth() - DirectoryOffset * (_directoryDepth + 1) - 5));
            
            if (_fileNameState == FileEditState.CreatingFolder || _fileNameState == FileEditState.CreatingFile)
                GUILayout.Label(_fileNameState == FileEditState.CreatingFolder ? "Folder:" : "File:", GetLabelStyle(), GUILayout.ExpandWidth(false));

            _newItemName = GUILayout.TextField(_newItemName, GetFileNameFieldStyle(), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("OK", GetButtonStyle(), GUILayout.ExpandWidth(false)))
            {
                if (_fileNameState == FileEditState.CreatingFolder || _fileNameState == FileEditState.CreatingFile)
                    CreateNewItem(_newItemName, _fileNameState == FileEditState.CreatingFolder);
                else if (_fileNameState == FileEditState.RenamingFile)
                    RenameActiveFile();
            }

            GUILayout.EndHorizontal();

            if (!_newItemErrorText.IsNullOrWhiteSpace())
                GUILayout.Label(_newItemErrorText, GetFileNameErrorStyle(), GUILayout.ExpandWidth(true));

            GUILayout.EndVertical();
        }

        private void RenameActiveFile()
        {
            if (_newItemName == Path.GetFileName(_activeFile))
            {
                SetFileEditState(FileEditState.None);
                return;
            }

            string directory = Path.GetDirectoryName(_activeFile);

            if (directory == null)
                return;

            string newPath = Path.Combine(directory, _newItemName);

            if (File.Exists(newPath))
            {
                _newItemErrorText = "File already exists";
                return;
            }
            
            try
            {
                File.Move(_activeFile, newPath);
                _activeFile = newPath;
                SetFileEditState(FileEditState.None);
            }
            catch (Exception e)
            {
                _newItemErrorText = e.Message;
            }
        }

        private void MoveActiveFileToTrash()
        {
            if (_activeFile.IsNullOrWhiteSpace())
                return;

            Directory.CreateDirectory(_trashBinDirectory);

            string filename = Path.Combine(_trashBinDirectory, Path.GetFileName(_activeFile));
            if (File.Exists(filename))
                filename = Path.Combine(_trashBinDirectory, $"{Path.GetFileNameWithoutExtension(filename)}_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(filename)}");

            try
            {
                if (_activeFile != null)
                    File.Move(_activeFile, filename);

                _activeFile = string.Empty;
                _fileContent = string.Empty;
                SetFileEditState(FileEditState.None);
            }
            catch (Exception e)
            {
                _newItemErrorText = e.Message;
            }
        }

        private void OpenTrashBin()
        {
            if (Directory.Exists(_trashBinDirectory))
                Process.Start(new ProcessStartInfo(_trashBinDirectory) { UseShellExecute = true });
        }

        private void DrawFiles(string path)
        {
            string[] files = Directory.GetFiles(path);

            bool newFileDrawed = false;
            foreach (string file in files)
                if (IsValidFile(file))
                {
                    if (file == _activeFile && _fileNameState == FileEditState.RenamingFile)
                        DrawFileNameField();
                    else if (GUILayout.Button(Path.GetFileName(file), GetFileStyle(file == _activeFile)))
                        LoadFileToEditor(file);

                    if (path == _activeDirectory && file == _activeFile && _fileNameState == FileEditState.CreatingFile)
                    {
                        DrawFileNameField();
                        newFileDrawed = true;
                    }
                }

            if (!newFileDrawed && path == _activeDirectory && _fileNameState == FileEditState.CreatingFile)
                DrawFileNameField();
        }

        private bool IsValidFile(string file)
        {
            string filename = Path.GetFileName(file);
            if (filename == "manifest.json")
                return false;

            if (_hideModConfigs.Value && Chainloader.PluginInfos.Values.Any(plugin => plugin.Instance.Config.ConfigFilePath == file))
                return false;

            string extension = Path.GetExtension(file).ToLower();
            if (_editableExtensions.Value.Split(',').Select(GetNormalizedExtension).Any(validExtension => extension == validExtension))
                return SearchString.IsNullOrWhiteSpace() || filename.IndexOf(SearchString, StringComparison.OrdinalIgnoreCase) > -1;

            return false;
        }

        private static string GetNormalizedExtension(string extension)
        {
            if (extension.IsNullOrWhiteSpace())
                return string.Empty;

            string ext = extension.Trim().ToLower();
            return ext.StartsWith('.') ? ext : "." + ext;
        }

        private bool DirectoryContainsValidFiles(string path)
        {
            return Directory.GetFiles(path).Any(IsValidFile) || Directory.GetDirectories(path).Any(DirectoryContainsValidFiles);
        }

        private void LoadFileToEditor(string filePath)
        {
            try
            {
                _fileContent = File.ReadAllText(filePath);
                _activeFile = filePath;
                _activeDirectory = Path.GetDirectoryName(filePath);
                _errorText = string.Empty;
                SetFileEditState(FileEditState.None);
                _focusTextArea = true;
            }
            catch (IOException e)
            {
                LogError($"Failed to load file {filePath}: {e.Message}");
                _errorText = "Failed to load file";
                _fileContent = e.Message;
            }
        }

        private void DrawDirectoriesMenu()
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New folder", GetButtonStyle()))
            {
                SetFileEditState(FileEditState.CreatingFolder);
            }
            if (GUILayout.Button("New file", GetButtonStyle()))
            {
                SetFileEditState(FileEditState.CreatingFile);
            }
            if (GUILayout.Button("Rename", GetButtonStyle()))
            {
                SetFileEditState(FileEditState.RenamingFile);
                _newItemName = Path.GetFileName(_activeFile);
            }
            if (GUILayout.Button(new GUIContent("Delete", "File will be moved into Trash Bin"), GetButtonStyle()))
                MoveActiveFileToTrash();

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _showEmptyFolders.Value = GUILayout.Toggle(_showEmptyFolders.Value, "Show empty folders", GetToggleStyle(), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Open Trash Bin", GetButtonStyle()))
                OpenTrashBin();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void CreateNewItem(string itemName, bool isFolder)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return;

            if (string.IsNullOrEmpty(_activeDirectory))
                return;

            string newPath = Path.Combine(_activeDirectory, itemName);

            try
            {
                if (isFolder)
                {
                    Directory.CreateDirectory(newPath);
                    _showEmptyFolders.Value = true;
                    _activeDirectory = newPath;
                    _activeFile = string.Empty;
                }
                else
                {
                    File.Create(newPath).Close();
                    LoadFileToEditor(newPath);
                }

                SetFileEditState(FileEditState.None);
            }
            catch (Exception e) 
            {
                _newItemErrorText = e.Message;
            }
        }

        public string IsValidJSON(string text)
        {
            if (text.IsNullOrWhiteSpace())
                return string.Empty;

            try
            {
                JToken.Parse(text);
                return "File is valid JSON";
            }
            catch
            {
                return "File is not valid JSON";
            }
        }

        public string IsValidYAML(string text)
        {
            if (text.IsNullOrWhiteSpace())
                return string.Empty;

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();

                deserializer.Deserialize<object>(text);

                return "File is valid YAML";
            }
            catch
            {
                return $"File is not valid YAML";
            }
        }

    }
}

