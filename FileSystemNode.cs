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
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDirectory { get; set; }
        public ComparisonStatus Status { get; set; }
        public int Depth { get; set; }

        public string DisplayName => new string(' ', Depth * 2) + (IsDirectory ? "📁 " : "📄 ") + Name;
        public string Symbol => ComparisonSymbol.GetSymbol(Status);
    }
}
