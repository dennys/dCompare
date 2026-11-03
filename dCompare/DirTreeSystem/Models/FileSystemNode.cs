namespace dCompare.DirTreeSystem.Models
{

    public class FileSystemNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime ModifiedTime { get; set; }
        public bool IsDirectory { get; set; }

        // Set on nodes built from an archive entry. Their FullPath is fabricated (archive file + entry key)
        // and cannot be opened, so content identity comes from Crc instead of a stream.
        public bool IsArchiveEntry { get; set; }
        public long? Crc { get; set; }

        // True when the folder could not be fully enumerated (access denied, folder removed mid-scan).
        // A partial tree must never be compared as if it were complete: the missing children would show up
        // as "only on the other side" and invite the user to delete files that are actually fine.
        public bool ScanIncomplete { get; set; }

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
