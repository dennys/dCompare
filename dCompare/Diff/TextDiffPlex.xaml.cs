using dCompare.Compare;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using DiffPlex.Model;
using DiffPlex.WindowsForms.Controls;
using DiffPlex.Wpf;
using DiffPlex.Wpf.Controls;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using dCompare.Viewer;
using System.Windows.Controls;
using System.Windows.Media;

namespace dCompare.Diff
{
    public partial class TextDiffPlex : UserControl, INotifyPropertyChanged, IRefreshable, IViewer
    {
        private string _leftPath = string.Empty;
        private string _rightPath = string.Empty;
        private string _leftText = string.Empty;
        private string _rightText = string.Empty;
        private ScrollViewer? _leftScrollViewer;
        private ScrollViewer? _rightScrollViewer;
        private bool _isSyncingScroll;

        public string LeftPath
        {
            get => _leftPath;
            set
            {
                _leftPath = value;
                OnPropertyChanged(nameof(LeftPath));
            }
        }

        public string RightPath
        {
            get => _rightPath;
            set
            {
                _rightPath = value;
                OnPropertyChanged(nameof(RightPath));
            }
        }

        public string LeftText
        {
            get => _leftText;
            set
            {
                _leftText = value;
                OnPropertyChanged(nameof(LeftText));
            }
        }

        public string RightText
        {
            get => _rightText;
            set
            {
                _rightText = value;
                OnPropertyChanged(nameof(RightText));
            }
        }

        public TextDiffPlex(string leftPath, string rightPath, object comparisonResult = null)
        {
            InitializeComponent();
            DataContext = this;
            Loaded += UcFileCompare_Loaded;

            LeftPath = leftPath;
            RightPath = rightPath;
            DiffView.IsCommandBarVisible = true;
            Refresh();
        }

        private void UcFileCompare_Loaded(object sender, RoutedEventArgs e)
        {
            // Find the internal TextBox controls within SideBySideDiffViewer
            _leftScrollViewer = FindScrollViewer(DiffView, "PART_OldText");
            if (_leftScrollViewer != null)
            {
                _leftScrollViewer.ScrollChanged += LeftScrollViewer_ScrollChanged;
            }

            _rightScrollViewer = FindScrollViewer(DiffView, "PART_NewText");
            if (_rightScrollViewer != null)
            {
                _rightScrollViewer.ScrollChanged += RightScrollViewer_ScrollChanged;
            }
        }
        private void DiffButton_Click(object sender, RoutedEventArgs e)
        {
            if (DiffView.IsInlineViewMode)
            {
                DiffView.ShowSideBySide();
                return;
            }

            DiffView.ShowInline();
        }

        private void FutherActionsButton_Click(object sender, RoutedEventArgs e)
        {
            DiffView.OpenViewModeContextMenu();
        }

        private void WindowButton_Click(object sender, RoutedEventArgs e)
        {
            var has = false;
            foreach (var w in Application.Current.Windows)
            {
                if (w is DiffWindow dw)
                {
                    dw.Activate();
                    has = true;
                    break;
                }
            }

            if (has) return;
            var win = new DiffWindow();
            win.OpenFileOnBoth();
            win.Show();
        }

        private ScrollViewer? FindScrollViewer(DependencyObject depObj, string name)
        {
            if (depObj is FrameworkElement element && element.Name == name)
            {
                return FindScrollViewer(depObj);
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = FindScrollViewer(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private ScrollViewer? FindScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = FindScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void LeftScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll || _rightScrollViewer == null)
                return;

            _isSyncingScroll = true;
            _rightScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            _rightScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            _isSyncingScroll = false;
        }

        private void RightScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isSyncingScroll || _leftScrollViewer == null)
                return;

            _isSyncingScroll = true;
            _leftScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            _leftScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            _isSyncingScroll = false;
        }
     
        public void Refresh()
        {
            if (string.IsNullOrEmpty(LeftPath) || string.IsNullOrEmpty(RightPath))
            {
                MessageBox.Show("Both file paths must be set to refresh the comparison.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Read file contents
                LeftText = File.ReadAllText(LeftPath);
                RightText = File.ReadAllText(RightPath);
                // SideBySideDiffViewer updates automatically via bindings
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh comparison: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void LoadFile(string filePath)
        {
            // In a comparison view, we load both files, so this method can be a no-op
            // or load the left file path.
            LeftPath = filePath;
            Refresh();
        }

        public void SaveFile(string? path = null)
        {
            throw new NotImplementedException();
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}