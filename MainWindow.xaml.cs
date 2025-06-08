using Serilog;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        private TabItem _draggedTab;
        private Point _dragStartPoint;
        private int _tabCounter = 1;

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            ConfigureLogging();

            // Initialize commands
            AddTabCommand = new RelayCommand(_ => AddNewTab(), _ => CanAddTab());
            CloseTabCommand = new RelayCommand(param => CloseTab(param as TabItem), param => CanCloseTab());

            DataContext = this;

            // Set initial status
            Log.Information("Application ready.");
            UpdateStatus("Application ready");
        }
        private void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.WithThreadId() // Adds Thread ID
                .WriteTo.File("logs\\dCompare.log",
                    rollingInterval: RollingInterval.Day, // Create a new log file daily
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss:fff} [ThreadId:{ThreadId}] {Level:u3}: {Message}{NewLine}")
                .CreateLogger();
        }
        #endregion

        #region Tab Management

        private void AddNewTab()
        {
            try
            {
                _tabCounter++;
                TabItem newTab = new TabItem
                {
                    Header = $"Tab {_tabCounter}",
                    Content = new UcCompare()
                };

                tabControl.Items.Add(newTab);
                tabControl.SelectedItem = newTab;

                UpdateStatus($"Added new tab: Tab {_tabCounter}");

                // Force command re-evaluation
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
                // If tab is null (e.g., from Ctrl+W), use the currently selected tab
                TabItem tabToClose = tab ?? tabControl.SelectedItem as TabItem;

                if (tabToClose == null || !tabControl.Items.Contains(tabToClose))
                {
                    UpdateStatus("No tab to close");
                    return;
                }

                // Don't show confirmation for the default "Welcome" tab or if there's only one tab
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

                // Store the index for potential selection adjustment
                int closedIndex = tabControl.Items.IndexOf(tabToClose);

                // Remove the tab
                tabControl.Items.Remove(tabToClose);

                // Select appropriate tab after closing
                if (tabControl.Items.Count > 0)
                {
                    // Select the tab that was after the closed one, or the last tab if we closed the last one
                    int newSelectedIndex = Math.Min(closedIndex, tabControl.Items.Count - 1);
                    tabControl.SelectedIndex = newSelectedIndex;
                }

                UpdateStatus($"Closed tab: {tabToClose.Header}");

                // Force command re-evaluation
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