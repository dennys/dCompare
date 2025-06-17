using System.ComponentModel;
using System.IO;

namespace dCompare.Compare
{
    public class TreeNode : INotifyPropertyChanged
    {
        private string _name;
        private string _relativePath;
        private bool _isDirectory;
        private int _depth;
        private bool _isExpanded;
        private FileSystemNode _leftNode;
        private FileSystemNode _rightNode;
        private ComparisonStatus _status;
        private string _leftName;
        private string _rightName;
        private string _leftSize;
        private string _rightSize;
        private string _leftDate;
        private string _rightDate;
        private string _statusSymbol;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string RelativePath
        {
            get => _relativePath;
            set { _relativePath = value; OnPropertyChanged(nameof(RelativePath)); }
        }

        public bool IsDirectory
        {
            get => _isDirectory;
            set { _isDirectory = value; OnPropertyChanged(nameof(IsDirectory)); }
        }

        public int Depth
        {
            get => _depth;
            set { _depth = value; OnPropertyChanged(nameof(Depth)); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public FileSystemNode LeftNode
        {
            get => _leftNode;
            set { _leftNode = value; OnPropertyChanged(nameof(LeftNode)); }
        }

        public FileSystemNode RightNode
        {
            get => _rightNode;
            set { _rightNode = value; OnPropertyChanged(nameof(RightNode)); }
        }

        public ComparisonStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public string LeftName
        {
            get => _leftName;
            set { _leftName = value; OnPropertyChanged(nameof(LeftName)); OnPropertyChanged(nameof(LeftDisplayName)); }
        }

        public string RightName
        {
            get => _rightName;
            set { _rightName = value; OnPropertyChanged(nameof(RightName)); OnPropertyChanged(nameof(RightDisplayName)); }
        }

        public string LeftSize
        {
            get => _leftSize;
            set { _leftSize = value; OnPropertyChanged(nameof(LeftSize)); }
        }

        public string RightSize
        {
            get => _rightSize;
            set { _rightSize = value; OnPropertyChanged(nameof(RightSize)); }
        }

        public string LeftDate
        {
            get => _leftDate;
            set { _leftDate = value; OnPropertyChanged(nameof(LeftDate)); }
        }

        public string RightDate
        {
            get => _rightDate;
            set { _rightDate = value; OnPropertyChanged(nameof(RightDate)); }
        }

        public string StatusSymbol
        {
            get => _statusSymbol;
            set { _statusSymbol = value; OnPropertyChanged(nameof(StatusSymbol)); }
        }

        public string LeftDisplayName => LeftName ?? string.Empty;
        public string RightDisplayName => RightName ?? string.Empty;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}