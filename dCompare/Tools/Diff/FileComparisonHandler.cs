using dCompare.Configuration;
using dCompare.Tools.Models;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System.IO;

namespace dCompare.Tools.Diff
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