using dCompare.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace dCompare.Tools.Diff
{
    public partial class TextDiff : UserControl, IRefreshable
    {
        private ScrollViewer? _leftScrollViewer;
        private ScrollViewer? _rightScrollViewer;
        private bool _isSyncingScroll;
        public TextDiffViewModel ViewModel { get; }

        public TextDiff(string leftPath, string rightPath, object comparisonResult)
        {
            InitializeComponent();
            ViewModel = new TextDiffViewModel(leftPath, rightPath, comparisonResult);
            DataContext = ViewModel;
            Loaded += UcFileCompare_Loaded;
        }

        private void UcFileCompare_Loaded(object sender, RoutedEventArgs e)
        {
            _leftScrollViewer = FindScrollViewer(LeftListBox);
            if (_leftScrollViewer != null)
            {
                _leftScrollViewer.ScrollChanged += LeftScrollViewer_ScrollChanged;
            }

            _rightScrollViewer = FindScrollViewer(RightListBox);
            if (_rightScrollViewer != null)
            {
                _rightScrollViewer.ScrollChanged += RightScrollViewer_ScrollChanged;
            }
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
            ViewModel.Refresh();
        }
    }
}