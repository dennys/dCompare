using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dCompare
{
    public class FileSystemNode
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public DateTime Created { get; set; }
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
        public ComparisonStatus Status { get; set; } = ComparisonStatus.Unknown;
        public ObservableCollection<FileSystemNode> Children { get; set; } = new();
        //public ObservableCollection<FileSystemNode> Children { get; set; } = new ObservableCollection<FileSystemNode>();
    }
}
