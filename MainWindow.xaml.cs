
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare
{
    public partial class MainWindow : Window
    {
        public ICommand AddTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        private TabItem _draggedTab;
        private Point _dragStartPoint;

        public MainWindow()
        {
            InitializeComponent();
            AddTabCommand = new RelayCommand(_ => AddNewTab());
            CloseTabCommand = new RelayCommand(param => CloseTab(param as TabItem), param => CanCloseTab());
            DataContext = this; // For command binding
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
        }
        private void AddNewTab()
        {
            TabItem newTab = new TabItem
            {
                Header = $"Tab {tabControl.Items.Count + 1}",
                Content = new UcCompare()
            };
            tabControl.Items.Add(newTab);
            tabControl.SelectedItem = newTab; // Select the new tab
        }

        private void CloseTab(TabItem tab)
        {
            // If tab is null (e.g., from Ctrl+W), use the currently selected tab
            TabItem tabToClose = tab ?? tabControl.SelectedItem as TabItem;
            if (tabToClose != null && tabControl.Items.Contains(tabToClose))
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to close '{tabToClose.Header}'?",
                    "Confirm Close",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    tabControl.Items.Remove(tabToClose);
                }
            }
        }
        // when I click '-' button, it works. but when I click ctrl-W, the 'tab' variable is null

        private bool CanCloseTab()
        {
            return tabControl.Items.Count > 1; // Prevent closing the last tab
        }

        // Drag-and-drop for tab reordering
        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggedTab = sender as TabItem;
            if (_draggedTab != null)
            {
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
                DragDrop.DoDragDrop(_draggedTab, _draggedTab, DragDropEffects.Move);
            }
        }

        private void TabItem_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(TabItem)) is TabItem sourceTab && sender is TabItem targetTab && sourceTab != targetTab)
            {
                int sourceIndex = tabControl.Items.IndexOf(sourceTab);
                int targetIndex = tabControl.Items.IndexOf(targetTab);

                tabControl.Items.Remove(sourceTab);
                tabControl.Items.Insert(targetIndex, sourceTab);
                tabControl.SelectedItem = sourceTab; // Keep the dragged tab selected
            }
            _draggedTab = null; // Reset dragged tab
        }
    }

    // Simple ICommand implementation for commands
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
    }
}