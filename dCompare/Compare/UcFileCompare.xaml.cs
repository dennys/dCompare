// File: UcFileCompare.xaml.cs
using DiffPlex.DiffBuilder.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Controls;

namespace dCompare.Compare
{
    public partial class UcFileCompare : UserControl, INotifyPropertyChanged
    {
        private string _leftPath;
        private string _rightPath;
        private ObservableCollection<DiffPiece> _leftLines;
        private ObservableCollection<DiffPiece> _rightLines;

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
        }

        public void SetComparisonResult(string leftPath, string rightPath, SideBySideDiffModel diffModel)
        {
            LeftPath = leftPath;
            RightPath = rightPath;
            LeftLines = new ObservableCollection<DiffPiece>(diffModel.OldText.Lines);
            RightLines = new ObservableCollection<DiffPiece>(diffModel.NewText.Lines);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}