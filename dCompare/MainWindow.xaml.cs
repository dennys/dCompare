using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare.Configuration;
using Serilog;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using dCompare.Tools.Viewer;

namespace dCompare
{
    public partial class MainWindow : Window
    {
        private TabItem? _draggedTab;
        private Point _dragStartPoint;
        private readonly MainWindowViewModel _viewModel;
        private readonly IConfigurationService _configService;

        public MainWindow(IConfigurationService configService)
        {
            InitializeComponent();

            // Resolve ViewModel using DI
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _viewModel = Ioc.Default.GetRequiredService<MainWindowViewModel>();
            DataContext = _viewModel;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            Log.Information("MainWindow created and initialized.");
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize session manager
            _viewModel.TabControl = tabControl;
            _viewModel._sessionManager.Initialize(tabControl, RecentSessionsMenu);
            await _viewModel.InitializeAsync();

            // Load and apply hotkey bindings
            await ApplyHotKeyBindingsAsync();
        }
        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            this.Closing -= MainWindow_Closing;
            e.Cancel = true; // Allow async operations
            var config = await _viewModel._configService.LoadConfigurationAsync();
            bool showConfirmation = config.Options?.Confirmation.CloseTab == true;

            if (showConfirmation)
            {
                bool confirmed = _viewModel._userInteractionService.ShowConfirmation(
                    Properties.Resources.ConfirmExit,
                    Properties.Resources.ConfirmExitTitle);
                if (!confirmed)
                {
                    this.Closing += MainWindow_Closing;
                    return;
                }
            }
            this.Close();
        }
        #region HotKey
        private async Task ApplyHotKeyBindingsAsync()
        {
            try
            {
                var config = await _configService.LoadConfigurationAsync();
                var hotKeys = config.Options?.HotKeys ?? new HotKeysOptions();

                InputBindings.Clear();

                // Helper method to add key binding
                void AddKeyBinding(string keyString, ICommand command)
                {
                    if (TryParseKeyGesture(keyString, out KeyGesture gesture))
                    {
                        InputBindings.Add(new KeyBinding(command, gesture));
                    }
                    else
                    {
                        Log.Warning($"Invalid hotkey format: {keyString}");
                        _viewModel.StatusMessage = $"Invalid hotkey format: {keyString}";
                    }
                }

                // Add key bindings from configuration
                AddKeyBinding(hotKeys.AddTab, _viewModel.AddTabCommand);
                AddKeyBinding(hotKeys.CloseTab, _viewModel.CloseTabCommand);
                AddKeyBinding(hotKeys.Compare, _viewModel.CompareCommand);
                AddKeyBinding(hotKeys.SaveSession, _viewModel.SaveSessionCommand);
                AddKeyBinding(hotKeys.LoadSession, _viewModel.LoadSessionCommand);
                AddKeyBinding(hotKeys.OpenOptions, _viewModel.OptionsCommand);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply hotkey bindings.");
                _viewModel.StatusMessage = $"Failed to apply hotkeys: {ex.Message}";
            }
        }

        private static bool TryParseKeyGesture(string keyString, out KeyGesture gesture)
        {
            gesture = null!;
            try
            {
                if (string.IsNullOrWhiteSpace(keyString))
                    return false;

                // Use KeyGestureConverter to parse the string (e.g., "Ctrl+N", "F6")
                KeyGestureConverter converter = new();

                if (converter.ConvertFromString(keyString) is KeyGesture parsedGesture)
                {
                    gesture = parsedGesture;
                }
                return gesture != null;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Drag and Drop

        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                TabItem? tabItem = FindAncestor<TabItem>(textBlock);
                if (tabItem != null)
                {
                    _draggedTab = tabItem;
                    _dragStartPoint = e.GetPosition(null);
                }
            }
        }

        private void TabItem_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                var tabItem = FindAncestor<TabItem>(textBlock);
                if (tabItem != null && tabControl != null)
                {
                    tabControl.SelectedItem = tabItem;
                    _viewModel.StatusMessage = $"Selected tab: {tabItem.Header}";
                }
            }
        }

        private void TabItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                TabItem? tabItem = FindAncestor<TabItem>(textBlock);
                if (tabItem != null && tabControl != null)
                {
                    tabControl.SelectedItem = tabItem;
                    _viewModel.StatusMessage = $"Selected tab: {tabItem.Header}";

                    if (textBlock.ContextMenu != null)
                    {
                        textBlock.ContextMenu.PlacementTarget = textBlock;
                        textBlock.ContextMenu.IsOpen = true;
                    }

                    e.Handled = true;
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
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
                    _viewModel.StatusMessage = $"Drag operation failed: {ex.Message}";
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

                        _viewModel.StatusMessage = $"Moved tab '{sourceTab.Header}' to position {targetIndex + 1}";
                    }
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Drop operation failed: {ex.Message}";
            }
            finally
            {
                _draggedTab = null;
            }
        }

        #endregion
    }
}