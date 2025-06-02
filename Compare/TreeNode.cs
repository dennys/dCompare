using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dCompare.Compare
{
    public class TreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded = false;

        public string Name { get; set; } = "";
        public string LeftName { get; set; } = "";
        public string RightName { get; set; } = "";
        public string LeftDisplayName => GetDisplayName(LeftName, Depth);
        public string RightDisplayName => GetDisplayName(RightName, Depth);
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

        public ObservableCollection<TreeNode> Children { get; set; } = new ObservableCollection<TreeNode>();

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

        public void ExpandAll()
        {
            IsExpanded = true;
            foreach (var child in Children)
            {
                child.ExpandAll();
            }
        }

        public void CollapseAll()
        {
            IsExpanded = false;
            foreach (var child in Children)
            {
                child.CollapseAll();
            }
        }

        private string GetDisplayName(string name, int depth)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // 根據深度添加縮排，每層縮排使用兩個空格
            string indent = new string(' ', depth * 6);

            // 添加圖標
            string icon = IsDirectory ? "📁 " : "📄 ";

            return icon + name;
        }
    }
}
