using dCompare.Compare;
using dCompare.Configuration;
using dCompare.Diff;
using dCompare.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace dCompare
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Properties and Fields

        public ICommand AddTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand SaveSessionCommand { get; }
        public ICommand LoadSessionCommand { get; }
        public ICommand ClearRecentSessionsCommand { get; }

        public ICommand OptionsCommand { get; }
        public ICommand CompareCommand { get; }
        public ICommand CopyToLeftCommand { get; }
        public ICommand CopyToRightCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ShowAllCommand { get; }
        public ICommand ShowDifferentCommand { get; }
        public ICommand ShowIdenticalCommand { get; }
        public ICommand ShowLeftUniqueCommand { get; }
        public ICommand ShowRightUniqueCommand { get; }

        public ICommand RenameTabCommand { get; }
        public ICommand AboutCommand { get; }
        public ICommand ExitCommand { get; }

        private readonly IConfigurationService _configService;
        private readonly ISessionManager _sessionManager;
        private readonly IServiceProvider _serviceProvider;

        private TabItem? _draggedTab;
        private Point _dragStartPoint;

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Constructor

        public MainWindow(
            IConfigurationService configService,
            ISessionManager sessionManager,
            IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _configService = configService;
            _sessionManager = sessionManager;
            _serviceProvider = serviceProvider;

            // Initialize session manager
            _sessionManager.Initialize(tabControl, RecentSessionsMenu);

            // Initialize commands
            AddTabCommand = new RelayCommand(_ => AddNewTab(), _ => CanAddTab());
            CloseTabCommand = new RelayCommand(param => CloseTab(param as TabItem), param => CanCloseTab());
            SaveSessionCommand = new RelayCommand(_ => _sessionManager.SaveSession(UpdateStatus), _ => _sessionManager.CanSaveSession());
            LoadSessionCommand = new RelayCommand(_ => _sessionManager.LoadSession(string.Empty, false, UpdateStatus, ExecuteUcCompareMethod), _ => _sessionManager.CanLoadSession());
            ClearRecentSessionsCommand = new RelayCommand(_ => _sessionManager.ClearRecentSessions(), _ => _sessionManager.CanClearRecentSessions());

            OptionsCommand = new RelayCommand(_ => ExecuteOptions(), _ => CanExecuteOptions());
            //CompareCommand = new RelayCommand(_ => ExecuteUcCompareMethod("Compare_Click"), _ => CanExecuteUcCompareMethod());
            CompareCommand = new RelayCommand(_ => ExecuteRefresh(), _ => CanExecuteRefresh());
            CopyToLeftCommand = new RelayCommand(_ => ExecuteUcCompareMethod("CopyToLeft_Click"), _ => CanExecuteUcCompareMethod());
            CopyToRightCommand = new RelayCommand(_ => ExecuteUcCompareMethod("CopyToRight_Click"), _ => CanExecuteUcCompareMethod());
            ExpandAllCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ExpandAll_Click"), _ => CanExecuteUcCompareMethod());
            CollapseAllCommand = new RelayCommand(_ => ExecuteUcCompareMethod("CollapseAll_Click"), _ => CanExecuteUcCompareMethod());
            ShowAllCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowAll_Click"), _ => CanExecuteUcCompareMethod());
            ShowDifferentCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowDifferent_Click"), _ => CanExecuteUcCompareMethod());
            ShowIdenticalCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowIdentical_Click"), _ => CanExecuteUcCompareMethod());
            ShowLeftUniqueCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowLeftUnique_Click"), _ => CanExecuteUcCompareMethod());
            ShowRightUniqueCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowRightUnique_Click"), _ => CanExecuteUcCompareMethod());

            RenameTabCommand = new RelayCommand(_ => ExecuteRenameTab(), _ => CanExecuteRenameTab());
            AboutCommand = new RelayCommand(_ => ExecuteAbout(), _ => CanExecuteAbout());
            ExitCommand = new RelayCommand(_ => ExecuteExit(), _ => CanExecuteExit());

            DataContext = this;

            Loaded += MainWindow_Loaded;
            Log.Information("MainWindow created and initialized.");
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AppConfiguration config = await _configService.LoadConfigurationAsync();
                if (config.Options?.Session.AutoLoadDefaultSession == true && !string.IsNullOrEmpty(config.Options.Session.DefaultSessionFile))
                {
                    _sessionManager.LoadSession(config.Options.Session.DefaultSessionFile, config.Options.Session.AutoCompareAfterSessionLoad, UpdateStatus, ExecuteUcCompareMethod);
                }
                else
                {
                    UpdateStatus(Properties.Resources.StatusAppReady);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading configuration: {ex.Message}");
                // Handle error, e.g., show error message to user
            }
        }

        #endregion

        private void ExecuteRefresh()
        {
            try
            {
                if (tabControl.SelectedItem is TabItem selectedTab)
                {
                    if (selectedTab.Content is UcCompare ucCompare)
                    {
                        MethodInfo methodInfo = typeof(UcCompare).GetMethod("Compare_Click", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (methodInfo != null)
                        {
                            methodInfo.Invoke(ucCompare, new object[] { null, null });
                            UpdateStatus("Compare executed");
                        }
                        else
                        {
                            MessageBox.Show("Compare_Click method not found", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (selectedTab.Content is UcFileCompare ucFileCompare)
                    {
                        MethodInfo methodInfo = typeof(UcFileCompare).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (methodInfo != null)
                        {
                            methodInfo.Invoke(ucFileCompare, new object[] { null });
                            UpdateStatus("Text comparison refreshed");
                        }
                        else
                        {
                            MessageBox.Show("Refresh method not found in UcFileCompare", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (selectedTab.Content is UcImageDiffViewer ucImageDiffViewer)
                    {
                        MethodInfo methodInfo = typeof(UcImageDiffViewer).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (methodInfo != null)
                        {
                            methodInfo.Invoke(ucImageDiffViewer, new object[] { null });
                            UpdateStatus("Image comparison refreshed");
                        }
                        else
                        {
                            MessageBox.Show("Refresh method not found in UcImageDiffViewer", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("No active tab or unsupported tab type", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("No active tab to execute refresh", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing refresh: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteRefresh()
        {
            return tabControl?.SelectedItem is TabItem selectedTab &&
                   (selectedTab.Content is UcCompare || selectedTab.Content is UcFileCompare || selectedTab.Content is UcImageDiffViewer);
        }

        #region Tab Management

        private async void AddNewTab()
        {
            try
            {
                _sessionManager.TabCounter++;
                var tabId = Guid.NewGuid().ToString();
                var newTab = new TabItem
                {
                    Header = $"Tab {_sessionManager.TabCounter}",
                    Content = _serviceProvider.GetRequiredService<UcCompare>()
                };
                tabControl.Items.Add(newTab);
                tabControl.SelectedItem = newTab;
                _sessionManager.TabIdMap[newTab.Header.ToString()!] = tabId;

                AppConfiguration config = await _configService.LoadConfigurationAsync();
                if (!config.Sessions.ContainsKey(tabId))
                {
                    await _configService.UpdateSessionAsync(tabId, string.Empty, string.Empty);
                }

                UpdateStatus($"Added new tab: Tab {_sessionManager.TabCounter}");
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.ErrorAddTab}: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CloseTab(TabItem? tab)
        {
            try
            {
                TabItem tabToClose = tab ?? (tabControl?.SelectedItem as TabItem);
                if (tabToClose == null || tabControl?.Items?.Contains(tabToClose) != true)
                {
                    UpdateStatus(Properties.Resources.StatusNoTabToClose);
                    return;
                }

                bool showConfirmation = tabControl.Items.Count > 1 &&
                                       !string.Equals(tabToClose.Header?.ToString(), "Welcome", StringComparison.OrdinalIgnoreCase);

                if (showConfirmation)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"{Properties.Resources.ConfirmCloseTab} '{tabToClose.Header}'?",
                        Properties.Resources.ConfirmCloseTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                int closedIndex = tabControl.Items.IndexOf(tabToClose);
                tabControl.Items.Remove(tabToClose);
                try
                {
                    var headerString = tabToClose.Header?.ToString();
                    if (headerString != null && _sessionManager.TabIdMap.TryGetValue(headerString, out string? tabId))
                    {
                        _sessionManager.TabIdMap.Remove(headerString);
                        await _configService.RemoveSessionAsync(tabId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to remove tab session: {Message}", ex.Message);
                    UpdateStatus($"Failed to remove tab session: {ex.Message}");
                }

                if (tabControl.Items.Count > 0)
                {
                    int newSelectedIndex = Math.Min(closedIndex, tabControl.Items.Count - 1);
                    tabControl.SelectedIndex = newSelectedIndex;
                }

                UpdateStatus($"Closed tab: {tabToClose.Header}");
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.ErrorCloseTab}: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanAddTab()
        {
            return tabControl?.Items.Count < 20;
        }

        private bool CanCloseTab() => tabControl?.Items.Count > 1; // Only allow closing when more than one tab exists


        private void ExecuteOptions()
        {
            try
            {
                var optionsWindow = new OptionsWindow();
                optionsWindow.Owner = this;
                optionsWindow.ShowDialog();
                UpdateStatus("Options window opened");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening options: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteOptions()
        {
            return true;
        }

        private void ExecuteRenameTab()
        {
            try
            {
                if (tabControl.SelectedItem is TabItem selectedTab)
                {
                    string currentName = selectedTab.Header?.ToString() ?? string.Empty;
                    string? newName = PromptForTabName(currentName);

                    if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
                    {
                        selectedTab.Header = newName;
                        string oldName = currentName;
                        if (_sessionManager.TabIdMap.TryGetValue(oldName, out string? tabId))
                        {
                            _sessionManager.TabIdMap.Remove(oldName);
                            _sessionManager.TabIdMap[newName] = tabId;
                        }
                        UpdateStatus($"Renamed tab to: {newName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming tab: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteRenameTab()
        {
            return tabControl?.SelectedItem is TabItem;
        }

        private void ExecuteAbout()
        {
            try
            {
                MessageBox.Show(
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
                    "About dCompare",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                UpdateStatus("About dialog opened");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening about dialog: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteAbout()
        {
            return true;
        }

        private void ExecuteExit()
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exiting application: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteExit()
        {
            return true;
        }

        private void ExecuteUcCompareMethod(string methodName)
        {
            try
            {
                if (tabControl.SelectedItem is TabItem selectedTab && selectedTab.Content is UcCompare ucCompare)
                {
                    MethodInfo methodInfo = typeof(UcCompare).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                    if (methodInfo != null)
                    {
                        methodInfo.Invoke(ucCompare, new object[] { null, null });
                        UpdateStatus($"{methodName.Replace("_Click", "")} executed");
                    }
                    else
                    {
                        MessageBox.Show($"{methodName} method not found", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("No active tab to execute action", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing {methodName}: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteUcCompareMethod()
        {
            return tabControl?.SelectedItem is TabItem selectedTab && selectedTab.Content is UcCompare;
        }

        #endregion

        #region Drag and Drop

        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                TabItem tabItem = FindAncestor<TabItem>(textBlock);
                if (tabItem != null)
                {
                    _draggedTab = tabItem;
                    _dragStartPoint = e.GetPosition(null);
                }
            }
        }

        // Helper method to find the parent TabItem
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
        }

        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedTab == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentPoint = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPoint;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                try
                {
                    DragDrop.DoDragDrop(_draggedTab, _draggedTab, DragDropEffects.Move);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Drag operation failed: {ex.Message}");
                }
                finally
                {
                    _draggedTab = null;
                }
            }
        }

        private void TabItem_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetData(typeof(TabItem)) is TabItem sourceTab &&
                    sender is TabItem targetTab &&
                    sourceTab != targetTab)
                {
                    int sourceIndex = tabControl.Items.IndexOf(sourceTab);
                    int targetIndex = tabControl.Items.IndexOf(targetTab);

                    if (sourceIndex >= 0 && targetIndex >= 0)
                    {
                        tabControl.Items.Remove(sourceTab);
                        tabControl.Items.Insert(targetIndex, sourceTab);
                        tabControl.SelectedItem = sourceTab;

                        UpdateStatus($"Moved tab '{sourceTab.Header}' to position {targetIndex + 1}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Drop operation failed: {ex.Message}");
            }
            finally
            {
                _draggedTab = null;
            }
        }

        #endregion

        #region Helper Methods

        private void UpdateStatus(string message)
        {
            if (StatusText != null)
            {
                StatusText.Text = message;
            }
        }

        private string PromptForTabName(string currentName)
        {
            return Microsoft.VisualBasic.Interaction.InputBox(
                Properties.Resources.RenameTabPrompt,
                Properties.Resources.RenameTabTitle,
                currentName);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Window Events

        protected override void OnClosing(CancelEventArgs e)
        {
            if (tabControl.Items.Count > 1)
            {
                MessageBoxResult result = MessageBox.Show(
                    Properties.Resources.ConfirmExit,
                    Properties.Resources.ConfirmExitTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnClosing(e);
        }

        #endregion
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            try
            {
                return _canExecute?.Invoke(parameter) ?? true;
            }
            catch
            {
                return false;
            }
        }

        public void Execute(object parameter)
        {
            try
            {
                _execute(parameter);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Command execution failed: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}