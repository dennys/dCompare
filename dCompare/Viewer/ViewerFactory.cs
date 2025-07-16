using dCompare.Configuration;
using System;
using System.IO;

namespace dCompare.Viewer
{
    public class ViewerFactory
    {
        private readonly IConfigurationService _configurationService;

        public ViewerFactory(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public IViewer CreateViewer(string filePath)
        {
            // For now, we only have a text viewer.
            // In the future, we can add more viewers here.
            return new TextViewer(_configurationService);
        }

        public IViewer CreateViewer(string leftFilePath, string rightFilePath)
        {
            var fileType = DetermineFileType(leftFilePath ?? rightFilePath);

            if (fileType == dCompare.Diff.Models.FileType.Text)
            {
                return new dCompare.Diff.TextDiffPlex(leftFilePath, rightFilePath, null);
            }
            else if (fileType == dCompare.Diff.Models.FileType.Image)
            {
                return new dCompare.Diff.ImageDiff(leftFilePath, rightFilePath);
            }
            else
            {
                throw new NotSupportedException("Unsupported file type for comparison.");
            }
        }

        private dCompare.Diff.Models.FileType DetermineFileType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            var textExtensions = new System.Collections.Generic.List<string> { ".txt", ".log", ".cs", ".json", ".xml", ".html", ".css", ".js", ".py", ".md", ".sql" };
            var imageExtensions = new System.Collections.Generic.List<string> { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".webp" };

            if (textExtensions.Contains(extension))
            {
                return dCompare.Diff.Models.FileType.Text;
            }

            if (imageExtensions.Contains(extension))
            {
                return dCompare.Diff.Models.FileType.Image;
            }

            return dCompare.Diff.Models.FileType.Binary;
        }
    }
}
