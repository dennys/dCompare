using dCompare.Configuration;
using System;
using System.IO;

namespace dCompare.Tools
{
    public class ToolFactory
    {
        private readonly IConfigurationService _configurationService;

        public ToolFactory(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public ITool CreateTool(string filePath)
        {
            var fileType = DetermineFileType(filePath);

            if (fileType == dCompare.Tools.Models.FileType.Image)
            {
                return new Tools.Viewer.ImageViewer(filePath);
            }

            // Fallback to text viewer for text and binary files
            return new Tools.Viewer.TextViewer(_configurationService);
        }

        public ITool CreateTool(string leftFilePath, string rightFilePath)
        {
            var fileType = DetermineFileType(leftFilePath ?? rightFilePath);

            if (fileType == dCompare.Tools.Models.FileType.Text)
            {
                return new dCompare.Tools.Diff.TextDiffPlex(leftFilePath, rightFilePath);
            }
            else if (fileType == dCompare.Tools.Models.FileType.Image)
            {
                return new dCompare.Tools.Diff.ImageDiff(leftFilePath, rightFilePath);
            }
            else
            {
                throw new NotSupportedException("Unsupported file type for comparison.");
            }
        }

        private static dCompare.Tools.Models.FileType DetermineFileType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            var textExtensions = new System.Collections.Generic.List<string> { ".txt", ".log", ".cs", ".json", ".xml", ".html", ".css", ".js", ".py", ".md", ".sql" };
            var imageExtensions = new System.Collections.Generic.List<string> { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".webp" };

            if (textExtensions.Contains(extension))
            {
                return dCompare.Tools.Models.FileType.Text;
            }

            if (imageExtensions.Contains(extension))
            {
                return dCompare.Tools.Models.FileType.Image;
            }

            return dCompare.Tools.Models.FileType.Binary;
        }
    }
}
