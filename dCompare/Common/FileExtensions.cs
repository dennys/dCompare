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

        private static bool HasExtensionIn(string path, HashSet<string> extensionSet)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path);
            return extensionSet.Contains(ext);
        }
    }
}
