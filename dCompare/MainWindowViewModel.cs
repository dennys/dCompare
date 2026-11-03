using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Common;
using dCompare.Compare;
using dCompare.Configuration;
using dCompare.FileOperation;
using dCompare.Options;
using dCompare.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace dCompare
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IConfigurationService _configService;
        private readonly ISessionManager _sessionManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUserInteractionService _userInteractionService;
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
            IServiceProvider serviceProvider,
            IUserInteractionService userInteractionService,
            ILogger<MainWindowViewModel> logger)
        {
            _configService = configService;
            _sessionManager = sessionManager;
            _serviceProvider = serviceProvider;
            _userInteractionService = userInteractionService;
            _logger = logger;
        }

        /// <summary>
        /// Hands the tab surface to the session manager. The View calls this once on load instead of reaching
        /// into the services the ViewModel holds, so the services stay private and testable.
        /// </summary>
        public void Initialize(TabControl tabControl, MenuItem recentSessionsMenu)
        {
            TabControl = tabControl;
            _sessionManager.Initialize(tabControl, recentSessionsMenu);
        }

        /// <summary>
        /// Asks the user before quitting, when Options | Confirmation asks for it. A corrupt configuration must
        /// not leave the window un-closeable, so a failure to read it continues without confirmation.
        /// </summary>
        public async Task<bool> ConfirmExitAsync()
        {
            try
            {
                AppConfiguration config = await _configService.LoadConfigurationAsync();
                // CloseApplication is the option the user actually ticks for "confirm before quitting";
                // CloseTab is a different setting (and also names a hotkey).
                if (config.Options?.Confirmation.CloseApplication != true)
                {
                    return true;
                }

                return _userInteractionService.ShowConfirmation(
                    Properties.Resources.ConfirmExit,
                    Properties.Resources.ConfirmExitTitle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read configuration while closing; continuing without confirmation.");
                return true;
            }
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
                    string defaultSessionFile = config.Options.Session.DefaultSessionFile;
                    if (!SessionPathGuard.IsAllowedDefaultSessionFile(defaultSessionFile))
                    {
                        // The default session file must live under the user profile: anything else
                        // (UNC, another drive) would auto-compare paths the user did not choose.
                        _logger.LogWarning("Default session file '{Path}' is not under the user profile; not loading it.", defaultSessionFile);
                        _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorDefaultSessionFileNotLocal, defaultSessionFile), Properties.Resources.ErrorTitle);
                        StatusMessage = Properties.Resources.StatusAppReady;
                    }
                    else
                    {
                        try
                        {
                            _sessionManager.LoadSession(defaultSessionFile, config.Options.Session.AutoCompareAfterSessionLoad, UpdateStatus, ExecuteSessionCompare);
                        }
                        catch (System.IO.FileNotFoundException)
                        {
                            _userInteractionService.ShowError(
                                string.Format(Properties.Resources.ErrorDefaultSessionFileNotExist, defaultSessionFile),
                                Properties.Resources.ErrorTitle);
                            StatusMessage = Properties.Resources.StatusAppReady;
                        }
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
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorLoadingConfiguration, ex.Message), Properties.Resources.ErrorTitle);
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
                    // Resolved from the provider (registered Transient in App) so each tab gets a new
                    // instance and tests can substitute their own CompareControl.
                    Content = _serviceProvider.GetRequiredService<CompareControl>()
                };
                TabControl.Items.Add(newTab);
                TabControl.SelectedItem = newTab;
                // Keyed by the TabItem instance (not the header): identical headers must not collide.
                _sessionManager.TabIdMap[newTab] = tabId;

                StatusMessage = string.Format(Properties.Resources.StatusAddedNewTab, $"Tab {_sessionManager.TabCounter}");
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorAddTab, ex.Message), Properties.Resources.ErrorTitle);
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
                                       !string.Equals(tabToClose.Header?.ToString(), Properties.Resources.WelcomeTabName, StringComparison.OrdinalIgnoreCase);

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
                    _sessionManager.TabIdMap.Remove(tabToClose);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove tab session");
                    StatusMessage = string.Format(Properties.Resources.StatusFailedToRemoveTabSession, ex.Message);
                }

                if (TabControl.Items.Count > 0)
                {
                    int newSelectedIndex = Math.Min(closedIndex, TabControl.Items.Count - 1);
                    TabControl.SelectedIndex = newSelectedIndex;
                }

                StatusMessage = string.Format(Properties.Resources.StatusClosedTab, tabToClose.Header);
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorCloseTab, ex.Message), Properties.Resources.ErrorTitle);
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
        [RelayCommand]
        public void LoadSessionFile((string filePath, bool autoCompare) parameters)
        {
            try
            {
                if (_sessionManager.CanLoadSession())
                {
                    string filePath = parameters.filePath ?? string.Empty;
                    bool autoCompare = parameters.autoCompare;
                    // Only report success when the session was actually loaded; LoadSession has
                    // already shown the specific error otherwise.
                    if (_sessionManager.LoadSession(filePath, autoCompare, UpdateStatus, ExecuteSessionCompare))
                    {
                        StatusMessage = string.Format(Properties.Resources.StatusLoadedSession, filePath, autoCompare);
                    }
                }
                else
                {
                    _userInteractionService.ShowError(Properties.Resources.ErrorCannotLoadSession, Properties.Resources.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session: {FilePath}, AutoCompare: {AutoCompare}", parameters.filePath, parameters.autoCompare);
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorFailedToLoadSession, ex.Message), Properties.Resources.ErrorTitle);
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
                StatusMessage = Properties.Resources.StatusOptionsWindowOpened;
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorOpeningOptions, ex.Message), Properties.Resources.ErrorTitle);
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
                    StatusMessage = Properties.Resources.StatusComparisonRefreshed;
                }
                else
                {
                    _userInteractionService.ShowError(Properties.Resources.ErrorNoActiveTabOrNoRefreshSupport, Properties.Resources.ErrorTitle);
                }
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorExecutingRefresh, ex.Message), Properties.Resources.ErrorTitle);
            }
        }

        private bool CanCompare() => TabControl?.SelectedItem is TabItem selectedTab && selectedTab.Content is IRefreshable;

        [RelayCommand]
        private void CopyToLeft() => ExecuteOnActiveCompare(nameof(CopyToLeft), c => c.CopyToLeft());

        [RelayCommand]
        private void CopyToRight() => ExecuteOnActiveCompare(nameof(CopyToRight), c => c.CopyToRight());

        [RelayCommand]
        private void ExpandAll() => ExecuteOnActiveCompare(nameof(ExpandAll), c => c.ExpandAll());

        [RelayCommand]
        private void CollapseAll() => ExecuteOnActiveCompare(nameof(CollapseAll), c => c.CollapseAll());

        [RelayCommand]
        private void ShowAll() => ExecuteOnActiveCompare(nameof(ShowAll), c => c.ShowAll());

        [RelayCommand]
        private void ShowDifferent() => ExecuteOnActiveCompare(nameof(ShowDifferent), c => c.ShowDifferent());

        [RelayCommand]
        private void ShowIdentical() => ExecuteOnActiveCompare(nameof(ShowIdentical), c => c.ShowIdentical());

        [RelayCommand]
        private void ShowLeftUnique() => ExecuteOnActiveCompare(nameof(ShowLeftUnique), c => c.ShowLeftUnique());

        [RelayCommand]
        private void ShowRightUnique() => ExecuteOnActiveCompare(nameof(ShowRightUnique), c => c.ShowRightUnique());

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
                        // The TabIdMap is keyed by the TabItem instance, so renaming needs no key migration.
                        selectedTab.Header = newName;
                        StatusMessage = string.Format(Properties.Resources.StatusRenamedTab, newName);
                    }
                }
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorRenamingTab, ex.Message), Properties.Resources.ErrorTitle);
            }
        }

        private bool CanRenameTab() => TabControl?.SelectedItem is TabItem;

        [RelayCommand]
        private void About()
        {
            try
            {
                _userInteractionService.ShowInformation(Properties.Resources.AboutMessage, Properties.Resources.AboutTitle);
                StatusMessage = Properties.Resources.StatusAboutDialogOpened;
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorOpeningAboutDialog, ex.Message), Properties.Resources.ErrorTitle);
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
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorExitingApplication, ex.Message), Properties.Resources.ErrorTitle);
            }
        }

        private void ExecuteOnActiveCompare(string actionName, Action<CompareControl> execute)
        {
            if (TabControl?.SelectedItem is TabItem { Content: CompareControl ucCompare })
            {
                ExecuteCompare(actionName, ucCompare, execute);
            }
            else
            {
                _userInteractionService.ShowError(Properties.Resources.ErrorNoActiveTabToExecuteAction, Properties.Resources.ErrorTitle);
            }
        }

        // "Compare" keeps the status text the old name-based dispatch produced for the auto-compare-after-load path.
        private void ExecuteSessionCompare(CompareControl ucCompare) => ExecuteCompare("Compare", ucCompare, c => c.Refresh());

        private void ExecuteCompare(string actionName, CompareControl ucCompare, Action<CompareControl> execute)
        {
            try
            {
                execute(ucCompare);
                StatusMessage = string.Format(Properties.Resources.StatusActionExecuted, actionName);
            }
            catch (Exception ex)
            {
                _userInteractionService.ShowError(string.Format(Properties.Resources.ErrorExecutingAction, actionName, ex.Message), Properties.Resources.ErrorTitle);
            }
        }

        private void UpdateStatus(string message)
        {
            StatusMessage = message;
        }
    }
}