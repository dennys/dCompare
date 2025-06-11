using System.ComponentModel;

namespace dCompare.Compare
{
    public class TreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded = true; // Default to expanded

        public string Name { get; set; } = "";
        public string LeftName { get; set; } = "";
        public string RightName { get; set; } = "";
        public string LeftDisplayName => GetDisplayName(LeftName);
        public string RightDisplayName => GetDisplayName(RightName);
        public string LeftSize { get; set; } = "";
        public string RightSize { get; set; } = "";
        public string LeftDate { get; set; } = "";
        public string RightDate { get; set; } = "";
        public string StatusSymbol { get; set; } = "";
        public ComparisonStatus Status { get; set; }
        public bool IsDirectory { get; set; }
        public string RelativePath { get; set; } = "";
        public int Depth { get; set; } = 0;

        public FileSystemNode? LeftNode { get; set; }
        public FileSystemNode? RightNode { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string GetDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string icon = IsDirectory ? "📁 " : "📄 ";
            return icon + name; // Indentation handled by DepthToMarginConverter
        }
    }
}