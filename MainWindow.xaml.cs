using dCompare.Configuration;
using dCompare.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Properties and Fields

        public ICommand AddTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand OptionsCommand { get; }
        public ICommand SaveSessionCommand { get; }
        public ICommand LoadSessionCommand { get; }
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

        private readonly ConfigurationService _configService;
        private readonly Dictionary<string, string> _tabIdMap; // Maps TabItem.Header to session ID

        private TabItem _draggedTab;
        private Point _dragStartPoint;
        private int _tabCounter = 1;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _tabIdMap = new Dictionary<string, string>();
            ConfigureLogging();

            // Initialize commands
            AddTabCommand = new RelayCommand(_ => AddNewTab(), _ => CanAddTab());
            CloseTabCommand = new RelayCommand(param => CloseTab(param as TabItem), param => CanCloseTab());
            OptionsCommand = new RelayCommand(_ => ExecuteOptions(), _ => CanExecuteOptions());
            CompareCommand = new RelayCommand(_ => ExecuteUcCompareMethod("Compare_Click"), _ => CanExecuteUcCompareMethod());
            CopyToLeftCommand = new RelayCommand(_ => ExecuteUcCompareMethod("CopyToLeft_Click"), _ => CanExecuteUcCompareMethod());
            CopyToRightCommand = new RelayCommand(_ => ExecuteUcCompareMethod("CopyToRight_Click"), _ => CanExecuteUcCompareMethod());
            ExpandAllCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ExpandAll_Click"), _ => CanExecuteUcCompareMethod());
            CollapseAllCommand = new RelayCommand(_ => ExecuteUcCompareMethod("CollapseAll_Click"), _ => CanExecuteUcCompareMethod());
            ShowAllCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowAll_Click"), _ => CanExecuteUcCompareMethod());
            ShowDifferentCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowDifferent_Click"), _ => CanExecuteUcCompareMethod());
            ShowIdenticalCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowIdentical_Click"), _ => CanExecuteUcCompareMethod());
            ShowLeftUniqueCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowLeftUnique_Click"), _ => CanExecuteUcCompareMethod());
            ShowRightUniqueCommand = new RelayCommand(_ => ExecuteUcCompareMethod("ShowRightUnique_Click"), _ => CanExecuteUcCompareMethod());
            SaveSessionCommand = new RelayCommand(_ => SaveSession(), _ => CanSaveSession());
            LoadSessionCommand = new RelayCommand(_ => LoadSession(), _ => CanLoadSession());

            DataContext = this;

            // Set initial status
            Log.Information("Application ready.");
            UpdateStatus("Application ready");
        }
        private void ConfigureLogging()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("dcompare.json", optional: true, reloadOnChange: true)
                .Build();

            var logConfig = configuration.GetSection("options:log").Get<LogConfig>() ?? new LogConfig();

            if (logConfig.Enabled)
            {
                var level = Enum.TryParse<LogEventLevel>(logConfig.Level, true, out var parsedLevel)
                    ? parsedLevel
                    : LogEventLevel.Information;

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Is(level)
                    .Enrich.WithThreadId()
                    .WriteTo.File(
                        path: "logs\\dCompare.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: logConfig.RetentionDays,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss:fff} [ThreadId:{ThreadId}] {Level:u3}: {Message}{NewLine}")
                    .CreateLogger();
            }
            else
            {
                Log.Logger = new LoggerConfiguration().CreateLogger(); // Null logger if disabled
            }
        }
        private void ApplyOptions(OptionsConfig options)
        {
            // Apply theme
            // Note: Implement theme switching logic based on options.Theme ("light" or "dark")
            // For example, update resource dictionaries or styles

            // Apply language
            // Note: Implement language switching logic based on options.Language
            // Example: Thread.CurrentThread.CurrentUICulture = new CultureInfo(options.Language);

            // Apply hotkeys
            UpdateHotKeys(options.HotKeys);
        }
        private void UpdateHotKeys(Dictionary<string, string> hotKeys)
        {
            // Clear existing key bindings  
            InputBindings.Clear();

            // Add new key bindings  
            foreach (var hotKey in hotKeys)
            {
                try
                {
                    // Fix for CS0120: Create an instance of KeyGestureConverter instead of using it statically  
                    var keyGestureConverter = new KeyGestureConverter();
                    if (keyGestureConverter.ConvertFromString(hotKey.Value) is KeyGesture keyGesture)
                    {
                        var command = hotKey.Key switch
                        {
                            "AddTab" => AddTabCommand,
                            "CloseTab" => CloseTabCommand,
                            "Compare" => CompareCommand,
                            // Add other commands as needed  
                            _ => null
                        };
                        if (command != null)
                        {
                            InputBindings.Add(new KeyBinding(command, keyGesture));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Invalid hotkey format for {Key}: {Value}", hotKey.Key, hotKey.Value);
                }
            }
        }
        #endregion
        #region Session Management

        private void SaveSession()
        {
            try
            {
                var config = new AppConfiguration();
                config.Options = _configService.GetConfiguration().Options;

                foreach (TabItem tab in tabControl.Items)
                {
                    if (tab.Content is UcCompare ucCompare)
                    {
                        string tabId = _tabIdMap.ContainsKey(tab.Header.ToString())
                            ? _tabIdMap[tab.Header.ToString()]
                            : Guid.NewGuid().ToString();

                        config.Sessions[tabId] = new SessionConfig
                        {
                            Left = ucCompare.LeftPathBox.Text,
                            Right = ucCompare.RightPathBox.Text
                        };

                        if (!_tabIdMap.ContainsKey(tab.Header.ToString()))
                        {
                            _tabIdMap[tab.Header.ToString()] = tabId;
                        }
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
                    var json = JsonSerializer.Serialize(config, options);
                    System.IO.File.WriteAllText(saveFileDialog.FileName, json);
                    UpdateStatus($"Session saved to {saveFileDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving session: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"Error saving session: {ex.Message}");
            }
        }

        private bool CanSaveSession()
        {
            return tabControl.Items.Count > 0;
        }

        private void LoadSession()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    DefaultExt = "json"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json);

                    if (config?.Sessions != null)
                    {
                        // Clear existing tabs except Welcome
                        while (tabControl.Items.Count > 1)
                        {
                            tabControl.Items.RemoveAt(tabControl.Items.Count - 1);
                        }
                        if (tabControl.Items.Count == 1 && (tabControl.Items[0] as TabItem)?.Header.ToString() == "Welcome")
                        {
                            tabControl.Items.Clear();
                        }

                        _tabIdMap.Clear();
                        _tabCounter = 0;

                        foreach (var session in config.Sessions)
                        {
                            _tabCounter++;
                            var tabId = session.Key;
                            var newTab = new TabItem
                            {
                                Header = $"Tab {_tabCounter}",
                                Content = new UcCompare()
                            };
                            ((UcCompare)newTab.Content).LeftPathBox.Text = session.Value.Left;
                            ((UcCompare)newTab.Content).RightPathBox.Text = session.Value.Right;
                            tabControl.Items.Add(newTab);
                            _tabIdMap[newTab.Header.ToString()] = tabId;
                        }

                        if (tabControl.Items.Count > 0)
                        {
                            tabControl.SelectedIndex = 0;
                        }

                        // Apply options
                        ApplyOptions(config.Options);

                        UpdateStatus($"Session loaded from {openFileDialog.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading session: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"Error loading session: {ex.Message}");
            }
        }

        private bool CanLoadSession()
        {
            return true;
        }

        #endregion

        #region Tab Management
        private void AddNewTab()
        {
            try
            {
                _tabCounter++;
                var tabId = Guid.NewGuid().ToString();
                var newTab = new TabItem
                {
                    Header = $"Tab {_tabCounter}",
                    Content = new UcCompare()
                };
                tabControl.Items.Add(newTab);
                tabControl.SelectedItem = newTab;
                _tabIdMap[newTab.Header.ToString()] = tabId;

                var config = _configService.GetConfiguration();
                if (!config.Sessions.ContainsKey(tabId))
                {
                    _configService.UpdateSession(tabId, string.Empty, string.Empty);
                }

                UpdateStatus($"Added new tab: Tab {_tabCounter}");
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseTab(TabItem tab)
        {
            try
            {
                TabItem tabToClose = tab ?? tabControl.SelectedItem as TabItem;
                if (tabToClose == null || !tabControl.Items.Contains(tabToClose))
                {
                    UpdateStatus("No tab to close");
                    return;
                }

                bool showConfirmation = tabControl.Items.Count > 1 &&
                                       !tabToClose.Header.ToString().Equals("Welcome", StringComparison.OrdinalIgnoreCase);

                if (showConfirmation)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"Are you sure you want to close '{tabToClose.Header}'?",
                        "Confirm Close",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                int closedIndex = tabControl.Items.IndexOf(tabToClose);
                var tabId = _tabIdMap[tabToClose.Header.ToString()];
                tabControl.Items.Remove(tabToClose);
                _tabIdMap.Remove(tabToClose.Header.ToString());
                _configService.RemoveSession(tabId);

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
                MessageBox.Show($"Error closing tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanAddTab()
        {
            // Allow adding tabs up to a reasonable limit
            return tabControl?.Items.Count < 20;
        }

        private bool CanCloseTab()
        {
            // Allow closing if there's more than one tab, or if the single tab isn't the welcome tab
            if (tabControl?.Items.Count == 0)
                return false;

            if (tabControl.Items.Count == 1)
            {
                var singleTab = tabControl.Items[0] as TabItem;
                return !singleTab?.Header.ToString().Equals("Welcome", StringComparison.OrdinalIgnoreCase) ?? false;
            }

            return true;
        }

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
                MessageBox.Show($"Error opening options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteOptions()
        {
            return true; // Options can always be opened
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
                        MessageBox.Show($"{methodName} method not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("No active tab to execute action", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing {methodName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (sender is TabItem tabItem)
            {
                _draggedTab = tabItem;
                _dragStartPoint = e.GetPosition(null);
            }
        }

        private void TabControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedTab == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentPoint = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPoint;

            // Start drag if mouse has moved sufficiently
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

        #region Menu Event Handlers

        private void RenameTab_Click(object sender, RoutedEventArgs e)
        {
            if (tabControl.SelectedItem is TabItem selectedTab)
            {
                string currentName = selectedTab.Header.ToString();
                string newName = PromptForTabName(currentName);

                if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
                {
                    selectedTab.Header = newName;
                    UpdateStatus($"Renamed tab to: {newName}");
                }
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "dCompare - Chrome-style Tabbed Interface\n\n" +
                "Features:\n" +
                "• Chrome-style tabs with hover effects\n" +
                "• Drag and drop tab reordering\n" +
                "• Keyboard shortcuts (Ctrl+N, Ctrl+W)\n" +
                "• Tab close confirmation\n" +
                "• Unified color scheme\n\n" +
                "Keyboard Shortcuts:\n" +
                "Ctrl+N: New Tab\n" +
                "Ctrl+W: Close Current Tab",
                "About dCompare",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (tabControl.Items.Count > 1)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Are you sure you want to exit? All tabs will be closed.",
                    "Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            Close();
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
            // Simple input dialog - in a real application, you might want a custom dialog
            return Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new tab name:",
                "Rename Tab",
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
                    "Are you sure you want to exit? All tabs will be closed.",
                    "Confirm Exit",
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

    #region Command Implementation

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
                MessageBox.Show($"Command execution failed: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion
}