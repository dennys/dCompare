using dCompare.Compare;
using dCompare.Configuration;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace dCompare
{
    public class SessionManager
    {
        private readonly ConfigurationService _configService;
        private readonly List<string> _recentSessions;
        private readonly Dictionary<string, string> _tabIdMap;
        private const int MaxRecentSessions = 5;
        private MenuItem _recentSessionsMenu;
        private TabControl _tabControl;
        private int _tabCounter;

        public SessionManager(ConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _recentSessions = new List<string>();
            _tabIdMap = new Dictionary<string, string>();
            _tabCounter = 1;
        }

        public void Initialize(TabControl tabControl, MenuItem recentSessionsMenu)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _recentSessionsMenu = recentSessionsMenu ?? throw new ArgumentNullException(nameof(recentSessionsMenu));
            LoadRecentSessions();
        }

        public Dictionary<string, string> TabIdMap => _tabIdMap;
        public int TabCounter { get => _tabCounter; set => _tabCounter = value; }

        private void LoadRecentSessions()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recentSessions.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var sessions = JsonSerializer.Deserialize<List<string>>(json);
                    if (sessions != null)
                    {
                        _recentSessions.AddRange(sessions);
                        UpdateRecentSessionsMenu();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to load recent sessions: {Message}", ex.Message);
            }
        }

        private void SaveRecentSessions()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "recentSessions.json");
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_recentSessions, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to save recent sessions: {Message}", ex.Message);
            }
        }

        public void AddRecentSession(string filePath)
        {
            try
            {
                _recentSessions.Remove(filePath);
                _recentSessions.Insert(0, filePath);
                while (_recentSessions.Count > MaxRecentSessions)
                {
                    _recentSessions.RemoveAt(_recentSessions.Count - 1);
                }
                SaveRecentSessions();
                UpdateRecentSessionsMenu();
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to add recent session: {Message}", ex.Message);
            }
        }

        public void ClearRecentSessions()
        {
            try
            {
                _recentSessions.Clear();
                SaveRecentSessions();
                UpdateRecentSessionsMenu();
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to clear recent sessions: {Message}", ex.Message);
            }
        }

        public bool CanClearRecentSessions()
        {
            return _recentSessions.Count > 0;
        }

        private void UpdateRecentSessionsMenu()
        {
            try
            {
                var itemsToRemove = new List<MenuItem>();
                foreach (var item in _recentSessionsMenu.Items)
                {
                    if (item is MenuItem menuItem && menuItem != _recentSessionsMenu.Items[0] && !(item is Separator))
                    {
                        itemsToRemove.Add(menuItem);
                    }
                }
                foreach (var item in itemsToRemove)
                {
                    _recentSessionsMenu.Items.Remove(item);
                }

                for (int i = 0; i < _recentSessions.Count; i++)
                {
                    string filePath = _recentSessions[i];
                    string fileName = Path.GetFileName(filePath);
                    var menuItem = new MenuItem
                    {
                        Header = $"{i + 1}. {fileName}",
                        ToolTip = filePath,
                        Command = Application.Current.MainWindow?.DataContext is MainWindow mainWindow ? mainWindow.LoadSessionCommand : null,
                        CommandParameter = filePath
                    };
                    _recentSessionsMenu.Items.Insert(i, menuItem);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to update recent sessions menu: {Message}", ex.Message);
            }
        }

        public void SaveSession(Action<string> updateStatus)
        {
            try
            {
                Dictionary<string, SessionConfig> sessions = new();

                foreach (TabItem tab in _tabControl.Items)
                {
                    if (tab.Content is UcCompare ucCompare)
                    {
                        string tabName = tab.Header.ToString();
                        sessions[tabName] = new SessionConfig
                        {
                            Left = ucCompare.LeftPathBox.Text,
                            Right = ucCompare.RightPathBox.Text,
                            Algorithm = ucCompare.SelectedAlgorithm
                        };
                    }
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    DefaultExt = "json",
                    FileName = "dCompareSession.json"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(sessions, options);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    AddRecentSession(saveFileDialog.FileName);
                    updateStatus($"Session saved to {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving session: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                updateStatus($"Error saving session: {ex.Message}");
            }
        }

        public bool CanSaveSession()
        {
            return _tabControl.Items.Count > 0;
        }

        public void LoadSession(string filePath = null, bool executeCompare = false, Action<string> updateStatus = null, Action<string> executeCompareMethod = null)
        {
            try
            {
                string json;
                string sessionFileName;

                if (string.IsNullOrEmpty(filePath))
                {
                    var openFileDialog = new OpenFileDialog
                    {
                        Filter = "JSON files (*.json)|*.json",
                        DefaultExt = "json"
                    };

                    if (openFileDialog.ShowDialog() != true)
                        return;

                    json = File.ReadAllText(openFileDialog.FileName);
                    sessionFileName = openFileDialog.FileName;
                }
                else
                {
                    if (!File.Exists(filePath))
                    {
                        updateStatus?.Invoke($"Session file not found: {filePath}");
                        return;
                    }
                    json = File.ReadAllText(filePath);
                    sessionFileName = filePath;
                }

                var sessions = JsonSerializer.Deserialize<Dictionary<string, SessionConfig>>(json);

                if (sessions != null)
                {
                    while (_tabControl.Items.Count > 1)
                    {
                        _tabControl.Items.RemoveAt(_tabControl.Items.Count - 1);
                    }
                    if (_tabControl.Items.Count == 1 && (_tabControl.Items[0] as TabItem)?.Header.ToString() == "Welcome")
                    {
                        _tabControl.Items.Clear();
                    }

                    _tabIdMap.Clear();
                    _tabCounter = 0;

                    foreach (var session in sessions)
                    {
                        _tabCounter++;
                        var tabName = session.Key;
                        var newTab = new TabItem
                        {
                            Header = tabName,
                            Content = new UcCompare()
                        };
                        ((UcCompare)newTab.Content).LeftPathBox.Text = session.Value.Left;
                        ((UcCompare)newTab.Content).RightPathBox.Text = session.Value.Right;
                        ((UcCompare)newTab.Content).SelectedAlgorithm = session.Value.Algorithm ?? "ModifiedDateAndSize";
                        _tabControl.Items.Add(newTab);
                        _tabIdMap[tabName] = Guid.NewGuid().ToString();

                        if (executeCompare && executeCompareMethod != null)
                        {
                            _tabControl.SelectedItem = newTab;
                            executeCompareMethod("Compare_Click");
                        }
                    }

                    if (_tabControl.Items.Count > 0)
                    {
                        _tabControl.SelectedIndex = 0;
                    }

                    AddRecentSession(sessionFileName);
                    updateStatus?.Invoke($"Session loaded from {sessionFileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading session: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                updateStatus?.Invoke($"Error loading session: {ex.Message}");
            }
        }

        public bool CanLoadSession()
        {
            return true;
        }
    }
}