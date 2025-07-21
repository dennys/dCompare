using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare.Common;
using dCompare.Compare;
using dCompare.Configuration;
using dCompare.FileOperation;
using Microsoft.Extensions.Logging;
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
            if (string.IsNullOrEmpty(leftFilePath) || string.IsNullOrEmpty(rightFilePath))
            {
                throw new ArgumentException("Both file paths cannot be null or empty.");
            }

            return fileType switch
            {
                Models.FileType.Text => new dCompare.Tools.Diff.TextDiffPlex(leftFilePath, rightFilePath),
                Models.FileType.Image => new dCompare.Tools.Diff.ImageDiff(leftFilePath, rightFilePath),
                Models.FileType.Archive => new CompareControl(
                    Ioc.Default.GetRequiredService<IConfigurationService>(),
                    Ioc.Default.GetRequiredService<IFileManager>(),
                    Ioc.Default.GetRequiredService<ILogger<CompareControl>>(),
                    leftFilePath, rightFilePath),
                _ => throw new NotSupportedException("Unsupported file type for comparison.")
            };
        }

        private static dCompare.Tools.Models.FileType DetermineFileType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (FileExtensions.IsTextFile(extension))
            {
                return dCompare.Tools.Models.FileType.Text;
            }

            if (FileExtensions.IsImageFile(extension))
            {
                return dCompare.Tools.Models.FileType.Image;
            }

            if (FileExtensions.IsArchiveFile(extension))
            {
                return dCompare.Tools.Models.FileType.Archive;
            }

            return dCompare.Tools.Models.FileType.Unsupported;
        }
    }
}
