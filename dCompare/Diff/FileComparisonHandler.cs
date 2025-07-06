using dCompare.Configuration;
using dCompare.Diff.Models;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System.IO;

namespace dCompare.Diff
{
    public class FileComparisonHandler
    {
        private readonly SideBySideDiffBuilder _diffBuilder;
        private readonly AppConfiguration _config;

        public FileComparisonHandler(AppConfiguration config)
        {
            _diffBuilder = new SideBySideDiffBuilder(new Differ());
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public FileType DetermineFileType(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return FileType.Unsupported;

            string? extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
                return FileType.Unsupported;

            // Check text file extensions from configuration
            if (_config.Options.Diff.Text.Ext.Contains(extension))
                return FileType.Text;

            // Check image file extensions from configuration
            if (_config.Options.Diff.Image.Ext.Contains(extension))
                return FileType.Image;

            // Future extensions: Add checks for additional file types (e.g., Zip, Rar)
            // Example: if (_config.Options.Diff.Zip?.Ext.Contains(extension)) return FileType.Zip;

            return FileType.Unsupported;
        }

        public async Task<SideBySideDiffModel> CompareTextFilesAsync(string leftPath, string rightPath)
        {
            try
            {
                string leftContent = await File.ReadAllTextAsync(leftPath);
                string rightContent = await File.ReadAllTextAsync(rightPath);
                return _diffBuilder.BuildDiffModel(leftContent, rightContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error comparing text files: {ex.Message}", ex);
            }
        }

        public Task<object> CompareImageFilesAsync(string leftPath, string rightPath)
        {
            throw new NotImplementedException("Image comparison is not yet implemented.");
        }

        public async Task<object> CompareFilesAsync(string leftPath, string rightPath, FileType fileType)
        {
            return fileType switch
            {
                FileType.Text => await CompareTextFilesAsync(leftPath, rightPath),
                FileType.Image => await CompareImageFilesAsync(leftPath, rightPath),
                // Future extensions: Add cases for new file types (e.g., FileType.Zip, FileType.Rar)
                _ => throw new NotSupportedException("Unsupported file type for comparison.")
            };
        }
    }
}