using System.IO;

namespace dCompare.Common
{
    public static class FileExtensions
    {
        private static readonly HashSet<string> _textExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".cs", ".csv", ".json", ".xml", ".html", ".css", ".js", ".py", ".md", ".sql"
        };
        private static readonly HashSet<string> _imageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp"
        };
        private static readonly HashSet<string> _archiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar"
        };

        public static IReadOnlyCollection<string> TextExtensions => _textExtensions;
        public static IReadOnlyCollection<string> ImageExtensions => _imageExtensions;
        public static IReadOnlyCollection<string> ArchiveExtensions => _archiveExtensions;

        public static bool IsTextFile(string path) => HasExtensionIn(path, _textExtensions);
        public static bool IsImageFile(string path) => HasExtensionIn(path, _imageExtensions);
        public static bool IsArchiveFile(string path) => HasExtensionIn(path, _archiveExtensions);

        /// <summary>
        /// The built-in viewers read a whole file into memory (and DiffPlex then builds two token models from it),
        /// so double-clicking a multi-GB log or a .raw mislabelled .txt in the compare list OOM-kills the app
        /// together with any copy that is running. Refuse those up front instead.
        /// </summary>
        public const long MaxViewableFileBytes = 100L * 1024 * 1024;

        public static bool IsTooLargeToView(string path, out long length, long maxBytes = MaxViewableFileBytes)
        {
            length = 0;
            try
            {
                length = new FileInfo(path).Length;
            }
            catch (IOException)
            {
                return false; // missing/locked: let the normal error path report it
            }
            return length > maxBytes;
        }

        /// <summary>
        /// True for junctions and directory symlinks, which must never be walked recursively:
        /// a link pointing back at an ancestor recurses until StackOverflowException kills the process
        /// (uncatchable, no log), and deleting/copying through a link touches files outside the compared tree.
        /// If the attributes cannot be read this returns false, so a directory is never silently skipped
        /// (a skipped directory would be reported as "missing on this side" and could get the user to delete good files).
        /// </summary>
        public static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasExtensionIn(string path, HashSet<string> extensionSet)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            return extensionSet.Contains(ext);
        }
    }
}
