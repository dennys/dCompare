using dCompare.Compare;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace dCompare.Diff
{
    public partial class UcTextDiffViewer : UserControl, INotifyPropertyChanged, IRefreshable
    {
        private string _leftPath = string.Empty;
        private string _rightPath = string.Empty;
        private ObservableCollection<DiffPiece> _leftLines;
        private ObservableCollection<DiffPiece> _rightLines;
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

        public UcTextDiffViewer(string leftPath, string rightPath, object comparisonResult)
        {
            InitializeComponent();
            DataContext = this;
            Loaded += UcFileCompare_Loaded;

            SetComparisonResult(leftPath, rightPath, comparisonResult);
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

        private void SetComparisonResult(string leftPath, string rightPath, object comparisonResult)
        {
            bool EnableVerboseLogging = false;

            LeftPath = leftPath;
            RightPath = rightPath;

            if (comparisonResult is SideBySideDiffModel diffModel)
            {
                var leftLines = new ObservableCollection<DiffPiece>();
                var rightLines = new ObservableCollection<DiffPiece>();

                foreach (var piece in diffModel.OldText.Lines)
                {
                    if (piece.SubPieces == null || piece.SubPieces.Count == 0)
                    {
                        piece.SubPieces = new List<DiffPiece> { new DiffPiece(piece.Text, piece.Type) };
                    }
                    leftLines.Add(piece);
                }

                foreach (var piece in diffModel.NewText.Lines)
                {
                    if (piece.SubPieces == null || piece.SubPieces.Count == 0)
                    {
                        piece.SubPieces = new List<DiffPiece> { new DiffPiece(piece.Text, piece.Type) };
                    }
                    rightLines.Add(piece);
                }

                LeftLines = leftLines;
                RightLines = rightLines;

                if (EnableVerboseLogging)
                {
                    //Debug: Log DiffPiece types and subpieces
                    foreach (var piece in LeftLines)
                    {
                        Debug.WriteLine($"Left DiffPiece: Text='{piece.Text}', Type={piece.Type}, SubPieces={piece.SubPieces?.Count ?? 0}");
                        if (piece.SubPieces != null)
                        {
                            foreach (var subPiece in piece.SubPieces)
                            {
                                Debug.WriteLine($"  SubPiece: Text='{subPiece.Text}', Type={subPiece.Type}");
                            }
                        }
                    }
                    foreach (var piece in RightLines)
                    {
                        Debug.WriteLine($"Right DiffPiece: Text='{piece.Text}', Type={piece.Type}, SubPieces={piece.SubPieces?.Count ?? 0}");
                        if (piece.SubPieces != null)
                        {
                            foreach (var subPiece in piece.SubPieces)
                            {
                                Debug.WriteLine($"  SubPiece: Text='{subPiece.Text}', Type={subPiece.Type}");
                            }
                        }
                    }
                }
            }
            else
            {
                LeftLines = new ObservableCollection<DiffPiece>();
                RightLines = new ObservableCollection<DiffPiece>();
            }
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(LeftPath) || string.IsNullOrEmpty(RightPath))
            {
                MessageBox.Show("Both file paths must be set to refresh the comparison.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Read file contents
                string leftText = File.ReadAllText(LeftPath);
                string rightText = File.ReadAllText(RightPath);

                // Compute diff using DiffPlex
                var diffBuilder = new SideBySideDiffBuilder();
                var diffModel = diffBuilder.BuildDiffModel(leftText, rightText);

                // Update the comparison result
                SetComparisonResult(LeftPath, RightPath, diffModel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh comparison: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // RelayCommand implementation for keybinding
        //private class RelayCommand : ICommand
        //{
        //    private readonly Action<object?> _execute;
        //    public RelayCommand(Action<object?> execute)
        //    {
        //        _execute = execute;
        //    }
        //    public bool CanExecute(object? parameter) => true;
        //    public void Execute(object? parameter) => _execute(parameter);
        //    public event EventHandler? CanExecuteChanged;
        //}
    }
}