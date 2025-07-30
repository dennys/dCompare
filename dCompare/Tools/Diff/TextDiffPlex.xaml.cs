using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace dCompare.Tools.Diff
{
    public partial class TextDiffPlex : UserControl, ITool
    {
        private ScrollViewer? _leftScrollViewer;
        private ScrollViewer? _rightScrollViewer;
        private bool _isSyncingScroll;

        public TextDiffPlex(string leftPath, string rightPath)
        {
            InitializeComponent();
            DataContext = new TextDiffPlexViewModel(leftPath, rightPath, DiffView);
            Loaded += UcFileCompare_Loaded;
        }

        private void UcFileCompare_Loaded(object sender, RoutedEventArgs e)
        {
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

        private static ScrollViewer? FindScrollViewer(DependencyObject depObj, string name)
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

        private static ScrollViewer? FindScrollViewer(DependencyObject depObj)
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

        public void LoadFile(string filePath)
        {
            // TODO this function is non-meaningful
            (DataContext as TextDiffPlexViewModel)?.LoadFile(filePath);
        }

        public void Refresh()
        {
            (DataContext as TextDiffPlexViewModel)?.Refresh();
        }

        public void SaveFile(string? path = null)
        {
            throw new System.NotImplementedException();
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}