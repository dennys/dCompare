using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dCompare.Compare
{

    public class FileSystemNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime ModifiedTime { get; set; }
        public bool IsDirectory { get; set; }

        public int Depth { get; set; } = 0; // 用於縮排顯示階層
        public List<FileSystemNode> Children { get; set; } = new List<FileSystemNode>();

        public override string ToString()
        {
            return $"{Name} ({(IsDirectory ? "DIR" : Size + " bytes")})";
        }

        public static string FormatSize(long size)
        {
            if (size < 1024) return $"{size} B";
            if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
            if (size < 1024 * 1024 * 1024) return $"{size / 1024.0 / 1024.0:F1} MB";
            return $"{size / 1024.0 / 1024.0 / 1024.0:F1} GB";
        }
    }
    //}
    //public class FileSystemNode
    //{
    //    public string Name { get; set; }
    //    public string FullPath { get; set; }
    //    public long Size { get; set; }
    //    public DateTime Modified { get; set; }
    //    public bool IsDirectory { get; set; }
    //    public ComparisonStatus Status { get; set; }
    //    public int Depth { get; set; }

    //    public string DisplayName => new string(' ', Depth * 2) + (IsDirectory ? "📁 " : "📄 ") + Name;
    //    public string Symbol => ComparisonSymbol.GetSymbol(Status);
    //}
}
