namespace dCompare.DirTreeSystem
{

    public class FileSystemNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime ModifiedTime { get; set; }
        public bool IsDirectory { get; set; }

        public int Depth { get; set; } = 0;
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
}
