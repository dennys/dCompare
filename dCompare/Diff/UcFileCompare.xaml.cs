// File: UcFileCompare.xaml.cs
using DiffPlex.DiffBuilder.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics; // For debugging
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace dCompare.Diff
{
    public partial class UcFileCompare : UserControl, INotifyPropertyChanged
    {
        private string _leftPath;
        private string _rightPath;
        private ObservableCollection<DiffPiece> _leftLines;
        private ObservableCollection<DiffPiece> _rightLines;
        private ScrollViewer _leftScrollViewer;
        private ScrollViewer _rightScrollViewer;
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

        public ObservableCollection<DiffPiece> LeftLines
        {
            get => _leftLines;
            set
            {
                _leftLines = value;
                OnPropertyChanged(nameof(LeftLines));
            }
        }

        public ObservableCollection<DiffPiece> RightLines
        {
            get => _rightLines;
            set
            {
                _rightLines = value;
                OnPropertyChanged(nameof(RightLines));
            }
        }

        public UcFileCompare()
        {
            InitializeComponent();
            DataContext = this;
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

        private ScrollViewer FindScrollViewer(DependencyObject depObj)
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

        public void SetComparisonResult(string leftPath, string rightPath, object comparisonResult)
        {
            LeftPath = leftPath;
            RightPath = rightPath;

            if (comparisonResult is SideBySideDiffModel diffModel)
            {
                LeftLines = new ObservableCollection<DiffPiece>(diffModel.OldText.Lines);
                RightLines = new ObservableCollection<DiffPiece>(diffModel.NewText.Lines);

                // Debug: Log DiffPiece types
                foreach (var piece in LeftLines)
                {
                    Debug.WriteLine($"Left DiffPiece: Text='{piece.Text}', Type={piece.Type}");
                }
                foreach (var piece in RightLines)
                {
                    Debug.WriteLine($"Right DiffPiece: Text='{piece.Text}', Type={piece.Type}");
                }
            }
            else
            {
                // Placeholder for other comparison types (e.g., images)
                LeftLines = new ObservableCollection<DiffPiece>();
                RightLines = new ObservableCollection<DiffPiece>();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}