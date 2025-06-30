// File: FileComparisonHandler.cs
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System;
using System.IO;
using System.Threading.Tasks;

namespace dCompare.Diff
{
    public enum FileType
    {
        Text,
        Image,
        Unsupported
    }

    public class FileComparisonHandler
    {
        private readonly ISideBySideDiffBuilder _diffBuilder;

        public FileComparisonHandler()
        {
            _diffBuilder = new SideBySideDiffBuilder(new Differ());
        }

        public FileType DetermineFileType(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return FileType.Unsupported;

            string? extension = Path.GetExtension(filePath)?.ToLowerInvariant();
            return extension switch
            {
                ".cs" or ".json" or ".log" or ".md" or ".txt" or ".xaml" or ".xml" => FileType.Text,
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" => FileType.Image,
                _ => FileType.Unsupported
            };
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
                _ => throw new NotSupportedException("Unsupported file type for comparison.")
            };
        }
    }
}