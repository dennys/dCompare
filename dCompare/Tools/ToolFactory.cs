using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare.Common;
using dCompare.Compare;
using dCompare.Configuration;
using dCompare.FileOperation;
using Microsoft.Extensions.Logging;
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
            var fileType = DetermineFileType(_configurationService.LoadConfigurationAsync().Result, filePath);

            if (fileType == "Image")
            {
                return new Viewer.ImageViewer(filePath);
            }

            // Fallback to text viewer for text and binary files
            return new Tools.Viewer.TextViewer(_configurationService);
        }

        public ITool CreateTool(string leftFilePath, string rightFilePath)
        {
            var fileType = DetermineFileType(_configurationService.LoadConfigurationAsync().Result, leftFilePath ?? rightFilePath);
            if (string.IsNullOrEmpty(leftFilePath) || string.IsNullOrEmpty(rightFilePath))
            {
                throw new ArgumentException(Properties.Resources.ErrorBothFilePathsNull);
            }

            switch (fileType)
            {
                case "Text":
                    return new Diff.TextDiffPlex(leftFilePath, rightFilePath);
                case "Image":
                    return new Diff.ImageDiff(leftFilePath, rightFilePath);
                case "Archive":
                    return new CompareControl(
                        Ioc.Default.GetRequiredService<IConfigurationService>(),
                        Ioc.Default.GetRequiredService<IFileManager>(),
                        Ioc.Default.GetRequiredService<ILogger<CompareControl>>(),
                        leftFilePath, rightFilePath);
                default:
                    // Fallback or throw exception for unsupported file types
                    // For example, fallback to a default text diff tool
                    return new Diff.TextDiffPlex(leftFilePath, rightFilePath);
            }
        }

        public string DetermineFileType(AppConfiguration config, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "Unsupported";

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            foreach (var mapping in config.Options.FileTypeMapping)
            {
                if (mapping.Value.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    return mapping.Key;
                }
            }

            return "Unsupported";
        }
    }
}
