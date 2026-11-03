using dCompare.Common;
using dCompare.Compare;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Serilog;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Session
{
    public class SessionManager(IServiceProvider serviceProvider, IUserInteractionService? userInteraction = null) : ISessionManager
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        private readonly IUserInteractionService _userInteraction = userInteraction ?? new UserInteractionService();
        private readonly List<string> _recentSessions = new();
        private readonly Dictionary<TabItem, string> _tabIdMap = new();
        private string _recentSessionsPath = string.Empty;
        private MenuItem? _recentSessionsMenu;
        private TabControl? _tabControl;
        private const int MaxRecentSessions = 5;
        private int _tabCounter = 1;

        public void Initialize(TabControl tabControl, MenuItem recentSessionsMenu)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _recentSessionsMenu = recentSessionsMenu ?? throw new ArgumentNullException(nameof(recentSessionsMenu));
            LoadRecentSessions();
        }

        public Dictionary<TabItem, string> TabIdMap => _tabIdMap;
        public int TabCounter { get => _tabCounter; set => _tabCounter = value; }

        private void LoadRecentSessions()
        {
            try
            {
                _recentSessionsPath = AppPaths.ResolveExisting(Constant.RecentSessionsFile);
                if (File.Exists(_recentSessionsPath))
                {
                    string json = File.ReadAllText(_recentSessionsPath);
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
                if (string.IsNullOrEmpty(_recentSessionsPath))
                {
                    _recentSessionsPath = AppPaths.Resolve(Constant.RecentSessionsFile);
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_recentSessions, options);
                try
                {
                    File.WriteAllText(_recentSessionsPath, json);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Read-only install folder: keep the recent list in %LOCALAPPDATA%\dCompare.
                    string fallbackPath = AppPaths.GetFallbackPath(_recentSessionsPath);
                    Log.Warning(ex, "Cannot write to '{Path}'; saving recent sessions to '{FallbackPath}' instead.", _recentSessionsPath, fallbackPath);
                    _recentSessionsPath = fallbackPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath)!);
                    File.WriteAllText(fallbackPath, json);
                }
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

        public bool CanClearRecentSessions() => _recentSessions.Count > 0;
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

        private void UpdateRecentSessionsMenu()
        {
            if (_recentSessionsMenu is null)
            {
                // The menu is wired once at startup; its absence is a wiring bug, not something the user can act on.
                Log.Warning("Recent-sessions menu is not available; the list was not refreshed.");
                return;
            }
            try
            {
                // Clear existing menu items (excluding the first item and separator)
                List<MenuItem> itemsToRemove = new();
                foreach (var item in _recentSessionsMenu.Items)
                {
                    if (item is MenuItem menuItem && menuItem != _recentSessionsMenu.Items[0] && item is not Separator)
                    {
                        itemsToRemove.Add(menuItem);
                    }
                }
                foreach (var item in itemsToRemove)
                {
                    _recentSessionsMenu.Items.Remove(item);
                }

                // Rebuild menu items for recent sessions
                for (int i = 0; i < _recentSessions.Count; i++)
                {
                    string filePath = _recentSessions[i];
                    MenuItem menuItem = new()
                    {
                        Header = $"{i + 1}. {filePath}",
                        ToolTip = filePath,
                        Command = Application.Current.MainWindow?.DataContext is MainWindowViewModel viewModel ? viewModel.LoadSessionFileCommand : null,
                        CommandParameter = (filePath, true)
                    };
                    _recentSessionsMenu.Items.Insert(i, menuItem);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to update recent sessions menu: {Message}", ex.Message);
            }
        }
        public bool CanSaveSession() => _tabControl?.Items.Count > 0;
        public void SaveSession(Action<string> updateStatus)
        {
            if (_tabControl is null)
            {
                MessageBox.Show(Properties.Resources.CannotLocateParentTabControl, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Dictionary<string, SessionConfig> sessions = new();

                foreach (TabItem tab in _tabControl.Items)
                {
                    if (tab.Content is CompareControl ucCompare)
                    {
                        string tabName = tab.Header?.ToString() ?? Properties.Resources.Untitled;
                        // Keyed by the TabItem instance (not the header): identical headers must not collide.
                        string tabId = _tabIdMap.TryGetValue(tab, out string? id) ? id : Guid.NewGuid().ToString();
                        _tabIdMap[tab] = tabId;
                        sessions[tabId] = new SessionConfig
                        {
                            TabName = tabName, // Save the tab header
                            Left = ucCompare.ViewModel.LeftPath,
                            Right = ucCompare.ViewModel.RightPath,
                            Algorithm = ucCompare.ViewModel.SelectedAlgorithm
                        };
                    }
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = Properties.Resources.JsonFiles,
                    DefaultExt = "json",
                    FileName = "dCompareSession.json"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(sessions, options);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    AddRecentSession(saveFileDialog.FileName);
                    updateStatus(string.Format(Properties.Resources.SessionSaved, saveFileDialog.FileName));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorSavingSession, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                updateStatus(string.Format(Properties.Resources.ErrorSavingSession, ex.Message));
            }
        }

        public bool CanLoadSession() => true;
        // Returns true only when the session was actually loaded; the caller must not report success otherwise.
        public bool LoadSession(string filePath, bool executeCompare = false, Action<string>? updateStatus = null, Action<CompareControl>? executeCompareMethod = null)
        {
            if (_tabControl is null)
            {
                MessageBox.Show(Properties.Resources.CannotLocateParentTabControl, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            try
            {
                string json;
                string sessionFileName;

                if (string.IsNullOrEmpty(filePath))
                {
                    var openFileDialog = new OpenFileDialog
                    {
                        Filter = Properties.Resources.JsonFiles,
                        DefaultExt = "json"
                    };

                    if (openFileDialog.ShowDialog() != true)
                        return false;

                    json = File.ReadAllText(openFileDialog.FileName);
                    sessionFileName = openFileDialog.FileName;
                }
                else
                {
                    if (!File.Exists(filePath))
                    {
                        updateStatus?.Invoke(string.Format(Properties.Resources.SessionFileNotFound, filePath));
                        throw new FileNotFoundException(string.Format(Properties.Resources.SessionFileNotFound, filePath));
                    }
                    json = File.ReadAllText(filePath);
                    sessionFileName = filePath;
                }

                var sessions = JsonSerializer.Deserialize<Dictionary<string, SessionConfig>>(json);

                if (sessions == null)
                {
                    updateStatus?.Invoke(string.Format(Properties.Resources.SessionFileEmpty, sessionFileName));
                    return false;
                }
                while (_tabControl.Items.Count > 1)
                {
                    _tabControl.Items.RemoveAt(_tabControl.Items.Count - 1);
                }
                if (_tabControl.Items.Count == 1 && string.Equals((_tabControl.Items[0] as TabItem)?.Header?.ToString(), Properties.Resources.WelcomeTabName, StringComparison.OrdinalIgnoreCase))
                {
                    _tabControl.Items.Clear();
                }

                _tabIdMap.Clear();
                _tabCounter = sessions.Count; // Set tab counter to the number of loaded tabs to avoid conflicts

                int tabsCreated = 0;
                foreach (var session in sessions)
                {
                    string leftPath = session.Value.Left ?? string.Empty;
                    string rightPath = session.Value.Right ?? string.Empty;

                    // Reject non-local (UNC) paths before anything touches them: auto-comparing a
                    // session entry would SMB-authenticate against an attacker share (Net-NTLM2).
                    string? rejectedPath = !SessionPathGuard.IsAllowedComparePath(leftPath) ? leftPath
                        : !SessionPathGuard.IsAllowedComparePath(rightPath) ? rightPath : null;
                    if (rejectedPath != null)
                    {
                        Log.Warning("Session entry '{TabName}' rejected: non-local path '{Path}'.", session.Value.TabName, rejectedPath);
                        _userInteraction.ShowError(string.Format(Properties.Resources.ErrorSessionPathRejected, rejectedPath), Properties.Resources.ErrorTitle);
                        updateStatus?.Invoke(string.Format(Properties.Resources.ErrorSessionPathRejected, rejectedPath));
                        continue; // no tab, no auto-compare
                    }

                    string tabName = session.Value.TabName ?? $"Tab {++_tabCounter}"; // Use saved tab name or generate a new one
                    CompareControl ucCompareInstance = _serviceProvider.GetRequiredService<CompareControl>();
                    TabItem newTab = new()
                    {
                        Header = tabName,
                        Content = ucCompareInstance
                    };
                    // Use the new properties to set paths, which will update the ViewModels
                    ucCompareInstance.ViewModel.SuppressLeftSuggestionsOnce();
                    ucCompareInstance.ViewModel.LeftPath = leftPath;
                    ucCompareInstance.ViewModel.SuppressRightSuggestionsOnce();
                    ucCompareInstance.ViewModel.RightPath = rightPath;
                    ucCompareInstance.ViewModel.SelectedAlgorithm = session.Value.Algorithm;

                    // The logic to keep suggestions closed is now handled within PathInputViewModel
                    // and CompareControl's LoadedLeftPath/LoadedRightPath setters via SuppressSuggestionsOnce().
                    // Thus, the explicit setting of IsSuggestionsOpen to false here is no longer needed and is removed.

                    _tabControl.Items.Add(newTab);
                    _tabIdMap[newTab] = session.Key;
                    tabsCreated++;

                    if (executeCompare && executeCompareMethod != null)
                    {
                        _tabControl.SelectedItem = newTab;
                        executeCompareMethod(ucCompareInstance);
                    }
                }

                if (_tabControl.Items.Count > 0)
                {
                    _tabControl.SelectedIndex = 0;
                }

                if (tabsCreated == 0)
                {
                    updateStatus?.Invoke(string.Format(Properties.Resources.SessionNoValidEntries, sessionFileName));
                    return false;
                }

                AddRecentSession(sessionFileName);
                updateStatus?.Invoke(string.Format(Properties.Resources.SessionLoaded, sessionFileName));
                return true;
            }
            catch (FileNotFoundException)
            {
                throw; // Re-throw to let MainWindow handle it
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorLoadingSession, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                updateStatus?.Invoke(string.Format(Properties.Resources.ErrorLoadingSession, ex.Message));
                return false;
            }
        }
    }
}
