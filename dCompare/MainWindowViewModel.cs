using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Compare;
using dCompare.Configuration;
using dCompare.Options;
using dCompare.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using dCompare.FileOperation;

namespace dCompare
{
    public partial class MainWindowViewModel : ObservableObject
    {
        public readonly IConfigurationService _configService;
        public readonly ISessionManager _sessionManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IToolService _toolService;
        public readonly IUserInteractionService _userInteractionService;
        private readonly ILogger<MainWindowViewModel> _logger;

        [ObservableProperty]
        private string _statusMessage = Properties.Resources.StatusAppReady;

        [ObservableProperty]
        private TabControl? _tabControl;
        partial void OnTabControlChanged(TabControl? value)
        {
            _logger.LogInformation("TabControl set to: {Value}", value != null ? "Non-null" : "Null");
        }

        [ObservableProperty]
        private string addTabHotkey = string.Empty;
        [ObservableProperty]
        private string closeTabHotkey = string.Empty;
        [ObservableProperty]
        private string refreshHotkey = string.Empty;
        [ObservableProperty]
        private string saveSessionHotkey = string.Empty;
        [ObservableProperty]
        private string loadSessionHotkey = string.Empty;

        [ObservableProperty]
        private string addTabTooltip = string.Empty;
        [ObservableProperty]
        private string closeTabTooltip = string.Empty;
        [ObservableProperty]
        private string refreshTooltip = string.Empty;
        [ObservableProperty]
        private string saveSessionTooltip = string.Empty;
        [ObservableProperty]
        private string loadSessionTooltip = string.Empty;
        [ObservableProperty]
        private string openOptionsHotkey = string.Empty;
        [ObservableProperty]
        private string openOptionsTooltip = string.Empty;

        partial void OnAddTabHotkeyChanged(string value) => UpdateTooltip();
        partial void OnCloseTabHotkeyChanged(string value) => UpdateTooltip();
        partial void OnRefreshHotkeyChanged(string value) => UpdateTooltip();
        partial void OnSaveSessionHotkeyChanged(string value) => UpdateTooltip();
        partial void OnLoadSessionHotkeyChanged(string value) => UpdateTooltip();
        partial void OnOpenOptionsHotkeyChanged(string value) => UpdateTooltip();

        private void UpdateTooltip()
        {
            AddTabTooltip = string.Format(CultureInfo.CurrentCulture, Properties.Resources.AddTabTooltip, AddTabHotkey);
            CloseTabTooltip = string.Format(CultureInfo.CurrentCulture, Properties.Resources.CloseTabTooltip, CloseTabHotkey);
            RefreshTooltip = string.Format(CultureInfo.CurrentCulture, Properties.Resources.CompareDirectoryMenu, RefreshHotkey);
            SaveSessionTooltip = string.Format(CultureInfo.CurrentCulture, Properties.Resources.SaveSessionMenu, SaveSessionHotkey);
            LoadSessionTooltip = string.Format(CultureInfo.CurrentCulture, Properties.Resources.LoadSessionMenu, LoadSessionHotkey);
            OpenOptionsTooltip = string.Format(CultureInfo.CurrentCulture, Properties.Resources.OptionsMenuTooltip, OpenOptionsHotkey);
        }

        public MainWindowViewModel(
            IConfigurationService configService,
            ISessionManager sessionManager,
            IToolService toolService,
            IServiceProvider serviceProvider,
            IUserInteractionService userInteractionService,
            ILogger<MainWindowViewModel> logger)
        {
            _configService = configService;
            _sessionManager = sessionManager;
            _toolService = toolService;
            _serviceProvider = serviceProvider;
            _userInteractionService = userInteractionService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                AppConfiguration config = await _configService.LoadConfigurationAsync();

                AddTabHotkey = config.Options.HotKeys.AddTab;
                CloseTabHotkey = config.Options.HotKeys.CloseTab;
                RefreshHotkey = config.Options.HotKeys.Compare;
                SaveSessionHotkey = config.Options.HotKeys.SaveSession;
                LoadSessionHotkey = config.Options.HotKeys.LoadSession;
                OpenOptionsHotkey = config.Options.HotKeys.OpenOptions;

                if (config.Options?.Session.AutoLoadDefaultSession == true && !string.IsNullOrEmpty(config.Options.Session.DefaultSessionFile))
                {
                    try
                    {
                        //await _sessionManager.LoadSessionAsync(config.Options.Session.DefaultSessionFile, config.Options.Session.AutoCompareAfterSessionLoad, UpdateStatus, ExecuteUcCompareMethod);
                        _sessionManager.LoadSession(config.Options.Session.DefaultSessionFile, config.Options.Session.AutoCompareAfterSessionLoad, UpdateStatus, ExecuteUcCompareMethod);
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        _userInteractionService.ShowError(
                            $"Default session file '{config.Options.Session.DefaultSessionFile}' does not exist. Please check the configuration or select a valid session file.",
                            Properties.Resources.ErrorTitle);
                        StatusMessage = Properties.Resources.StatusAppReady;
                    }
                }
                else
                {
                    StatusMessage = Properties.Resources.StatusAppReady;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration");
                _userInteractionService.ShowError($"Error loading configuration: {ex.Message}", Properties.Resources.ErrorTitle);
            }
            _logger.LogInformation("MainWindowViewModel created and initialized.");
        }
        [RelayCommand(CanExecute = nameof(CanAddTab))]
        private void AddTab()
        {
            try
            {
                if (TabControl == null) return;

                _sessionManager.TabCounter++;
                var tabId = Guid.NewGuid().ToString();
                var newTab = new TabItem
                {
                    Header = $"Tab {_sessionManager.TabCounter}",
                    Content = new CompareControl(
                        _serviceProvider.GetRequiredService<IConfigurationService>(),
                        _toolService,
                        _serviceProvider.GetRequiredService<IFileManager>(),
                        _serviceProvider.GetRequiredService<ILogger<CompareControl>>())
                };
                TabControl.Items.Add(newTab);
                TabControl.SelectedItem = newTab;
                _sessionManager.TabIdMap[newTab.Header.ToString()!] = tabId;

                StatusMessage = $"Added new tab: Tab {_sessionManager.TabCounter}";
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"{Properties.Resources.ErrorAddTab}: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }
        private bool CanAddTab() => TabControl == null || TabControl.Items.Count < 20; // TabControl needs to be checked or it will alreays return flase

        [RelayCommand(CanExecute = nameof(CanCloseTab))]
        private async Task CloseTab(TabItem? tab)
        {
            try
            {
                if (TabControl == null) return;

                TabItem? tabToClose = tab ?? TabControl.SelectedItem as TabItem;
                if (tabToClose == null || !TabControl.Items.Contains(tabToClose))
                {
                    StatusMessage = Properties.Resources.StatusNoTabToClose;
                    return;
                }

                AppConfiguration config = await _configService.LoadConfigurationAsync();
                bool showConfirmation = config.Options?.Confirmation.CloseTab == true &&
                                       TabControl.Items.Count > 1 &&
                                       !string.Equals(tabToClose.Header?.ToString(), "Welcome", StringComparison.OrdinalIgnoreCase);

                if (showConfirmation)
                {
                    bool confirmed = _userInteractionService.ShowConfirmation(
                        $"{Properties.Resources.ConfirmCloseTab} '{tabToClose.Header}'?",
                        Properties.Resources.ConfirmCloseTitle);
                    if (!confirmed) return;
                }

                int closedIndex = TabControl.Items.IndexOf(tabToClose);
                TabControl.Items.Remove(tabToClose);
                try
                {
                    var headerString = tabToClose.Header?.ToString();
                    if (headerString != null && _sessionManager.TabIdMap.TryGetValue(headerString, out string? tabId))
                    {
                        _sessionManager.TabIdMap.Remove(headerString);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove tab session");
                    StatusMessage = $"Failed to remove tab session: {ex.Message}";
                }

                if (TabControl.Items.Count > 0)
                {
                    int newSelectedIndex = Math.Min(closedIndex, TabControl.Items.Count - 1);
                    TabControl.SelectedIndex = newSelectedIndex;
                }

                StatusMessage = $"Closed tab: {tabToClose.Header}";
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"{Properties.Resources.ErrorCloseTab}: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        private bool CanCloseTab() => TabControl == null || TabControl?.Items.Count > 1;

        [RelayCommand]
        private void SaveSession()
        {
            if (_sessionManager.CanSaveSession())
            {
                _sessionManager.SaveSession(UpdateStatus);
            }
        }
        [RelayCommand]
        public void LoadSession()
        {
            _sessionManager.LoadSession(string.Empty);
        }

        public static (string filePath, bool autoCompare) LoadSessionArgs => (string.Empty, true);
        //public async Task LoadSession(string file, bool executeCompare = false, Action<string>? updateStatus = null, Action<string>? executeCompareMethod = null)
        [RelayCommand]
        public void LoadSessionFile((string filePath, bool autoCompare) parameters)
        {
            try
            {
                if (_sessionManager.CanLoadSession())
                {
                    string filePath = parameters.filePath ?? string.Empty;
                    bool autoCompare = parameters.autoCompare;
                    _sessionManager.LoadSession(filePath, autoCompare, UpdateStatus, ExecuteUcCompareMethod);
                    StatusMessage = $"Loaded session: {filePath} (AutoCompare: {autoCompare})";
                }
                else
                {
                    _userInteractionService.ShowError("Cannot load session at this time.", Properties.Resources.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session: {FilePath}, AutoCompare: {AutoCompare}", parameters.filePath, parameters.autoCompare);
                _userInteractionService.ShowError($"Failed to load session: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        [RelayCommand]
        private void ClearRecentSessions()
        {
            if (_sessionManager.CanClearRecentSessions())
            {
                _sessionManager.ClearRecentSessions();
            }
        }

        [RelayCommand]
        private void Options()
        {
            try
            {
                var optionsWindow = new OptionsWindow();
                optionsWindow.ShowDialog();
                StatusMessage = "Options window opened";
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"Error opening options: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        [RelayCommand]
        private void Compare()
        {
            try
            {
                if (TabControl?.SelectedItem is TabItem selectedTab && selectedTab.Content is IRefreshable refreshable)
                {
                    refreshable.Refresh();
                    StatusMessage = "Comparison refreshed";
                }
                else
                {
                    _userInteractionService.ShowError("No active tab or tab does not support refresh", Properties.Resources.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"Error executing refresh: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        private bool CanCompare() => TabControl?.SelectedItem is TabItem selectedTab && selectedTab.Content is IRefreshable;

        [RelayCommand]
        private void CopyToLeft() => ExecuteUcCompareMethod("CopyToLeft_Click");

        [RelayCommand]
        private void CopyToRight() => ExecuteUcCompareMethod("CopyToRight_Click");

        [RelayCommand]
        private void ExpandAll() => ExecuteUcCompareMethod("ExpandAll_Click");

        [RelayCommand]
        private void CollapseAll() => ExecuteUcCompareMethod("CollapseAll_Click");

        [RelayCommand]
        private void ShowAll() => ExecuteUcCompareMethod("ShowAll_Click");

        [RelayCommand]
        private void ShowDifferent() => ExecuteUcCompareMethod("ShowDifferent_Click");

        [RelayCommand]
        private void ShowIdentical() => ExecuteUcCompareMethod("ShowIdentical_Click");

        [RelayCommand]
        private void ShowLeftUnique() => ExecuteUcCompareMethod("ShowLeftUnique_Click");

        [RelayCommand]
        private void ShowRightUnique() => ExecuteUcCompareMethod("ShowRightUnique_Click");

        private bool CanExecuteUcCompareMethod() => TabControl?.SelectedItem is TabItem selectedTab && selectedTab.Content is CompareControl;

        [RelayCommand]
        private void RenameTab()
        {
            try
            {
                if (TabControl?.SelectedItem is TabItem selectedTab)
                {
                    string currentName = selectedTab.Header?.ToString() ?? string.Empty;
                    string newName = _userInteractionService.PromptForTabName(currentName, Properties.Resources.RenameTabPrompt, Properties.Resources.RenameTabTitle);

                    if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
                    {
                        selectedTab.Header = newName;
                        string oldName = currentName;
                        if (_sessionManager.TabIdMap.TryGetValue(oldName, out string? tabId))
                        {
                            _sessionManager.TabIdMap.Remove(oldName);
                            _sessionManager.TabIdMap[newName] = tabId;
                        }
                        StatusMessage = $"Renamed tab to: {newName}";
                    }
                }
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"Error renaming tab: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        private bool CanRenameTab() => TabControl?.SelectedItem is TabItem;

        [RelayCommand]
        private void About()
        {
            try
            {
                _userInteractionService.ShowInformation(
                    "dCompare - Chrome-style Tabbed Interface\n\n" +
                    "Features:\n" +
                    "• Chrome-style tabs with hover effects\n" +
                    "• Drag and drop tab reordering\n" +
                    "• Keyboard shortcuts (Ctrl+N, Ctrl+W)\n" +
                    "• Tab close confirmation\n" +
                    "• Unified color scheme\n" +
                    "• Recent sessions history\n\n" +
                    "Keyboard Shortcuts:\n" +
                    "Ctrl+N: New Tab\n" +
                    "Ctrl+W: Close Current Tab\n" +
                    "Ctrl+S: Save Session\n" +
                    "Ctrl+O: Load Session",
                    "About dCompare");
                StatusMessage = "About dialog opened";
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"Error opening about dialog: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        [RelayCommand]
        private void Exit()
        {
            try
            {
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"Error exiting application: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        private void ExecuteUcCompareMethod(string methodName)
        {
            try
            {
                if (TabControl?.SelectedItem is TabItem selectedTab && selectedTab.Content is CompareControl ucCompare)
                {
                    MethodInfo? methodInfo = typeof(CompareControl).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                    if (methodInfo != null)
                    {
                        methodInfo.Invoke(ucCompare, new object[] { ucCompare, new RoutedEventArgs() });
                        StatusMessage = $"{methodName.Replace("_Click", "")} executed";
                    }
                    else
                    {
                        _userInteractionService.ShowError($"{methodName} method not found", Properties.Resources.ErrorTitle);
                    }
                }
                else
                {
                    _userInteractionService.ShowError("No active tab to execute action", Properties.Resources.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError($"Error executing {methodName}: {ex.Message}", Properties.Resources.ErrorTitle);
            }
        }

        private void UpdateStatus(string message)
        {
            StatusMessage = message;
        }
    }
}