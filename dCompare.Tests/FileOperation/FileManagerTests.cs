using dCompare.Compare;
using dCompare.FileOperation;
using dCompare.FileOperation.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.FileOperation
{
    public class FileManagerTests : IAsyncLifetime
    {
        private string _sourceDir;
        private string _targetDir;
        private readonly FileManager _fileManager;

        public FileManagerTests()
        {
            _fileManager = new FileManager();
            _sourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _targetDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        public async Task InitializeAsync()
        {
            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_targetDir);

            // Create test files
            await CreateDummyFile(Path.Combine(_sourceDir, "largefile.bin"), 50 * 1024 * 1024); // Increased to 50 MB
            File.WriteAllText(Path.Combine(_sourceDir, "file1.txt"), "hello");
            File.WriteAllText(Path.Combine(_sourceDir, "file2.txt"), "world");
            Directory.CreateDirectory(Path.Combine(_sourceDir, "EmptySubDir"));
        }

        public Task DisposeAsync()
        {
            try
            {
                if (Directory.Exists(_sourceDir))
                    Directory.Delete(_sourceDir, true);
                if (Directory.Exists(_targetDir))
                    Directory.Delete(_targetDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
            return Task.CompletedTask;
        }

        [Fact]
        public async Task CopyAsync_SingleLargeFile_ReportsProgressAndCompletes()
        {
            // Arrange
            var sourceFile = Path.Combine(_sourceDir, "largefile.bin");
            var targetFile = Path.Combine(_targetDir, "largefile_copy.bin");
            var progressReports = new List<OperationProgress>();
            var progress = new Progress<OperationProgress>(p => progressReports.Add(p));
            var cts = new CancellationTokenSource();

            // Act
            var result = await _fileManager.CopyAsync(sourceFile, targetFile, true, progress, cts.Token);

            // Assert
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(0, result.FailCount);
            Assert.Empty(result.FailedItems);
            Assert.True(result.ExecutionTime > TimeSpan.Zero);
            Assert.True(File.Exists(targetFile));
            Assert.True(progressReports.Count > 0, "Expected progress reports");
            Assert.Contains(progressReports, p => p.Percentage == 100);
        }

        [Fact]
        public async Task CopyAsync_Cancelled_ThrowsOperationCanceledException()
        {
            // Arrange
            var sourceFile = Path.Combine(_sourceDir, "largefile.bin");
            var targetFile = Path.Combine(_targetDir, "largefile_copy.bin");
            var cts = new CancellationTokenSource();
            cts.CancelAfter(50); // Reduced to 50ms for faster cancellation

            // Act & Assert
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _fileManager.CopyAsync(sourceFile, targetFile, true, null, cts.Token));

            // Additional verification
            Assert.False(File.Exists(targetFile), "Target file should not exist after cancellation");
        }

        [Fact]
        public async Task CopyMultipleAsync_MixedItems_SucceedsAndFailsCorrectly()
        {
            // Arrange
            var itemsToCopy = new List<(string source, string target, SourceType type)>
            {
                (Path.Combine(_sourceDir, "file1.txt"), Path.Combine(_targetDir, "file1_copy.txt"), SourceType.Left),
                (Path.Combine(_sourceDir, "file2.txt"), Path.Combine(_targetDir, "file2_copy.txt"), SourceType.Left),
                (Path.Combine(_sourceDir, "non_existent_file.txt"), Path.Combine(_targetDir, "fail.txt"), SourceType.Left),
                (Path.Combine(_sourceDir, "EmptySubDir"), Path.Combine(_targetDir, "EmptySubDir_copy"), SourceType.Left)
            };
            var progressReports = new List<OperationProgress>();
            var progress = new Progress<OperationProgress>(p => progressReports.Add(p));

            // Act
            var result = await _fileManager.CopyMultipleAsync(itemsToCopy, true, progress);

            // Assert
            Assert.Equal(3, result.SuccessCount); // file1, file2, EmptySubDir
            Assert.Equal(1, result.FailCount); // non_existent_file
            Assert.Contains(Path.Combine(_sourceDir, "non_existent_file.txt"), result.FailedItems);
            Assert.True(result.ExecutionTime > TimeSpan.Zero);
            Assert.True(File.Exists(Path.Combine(_targetDir, "file1_copy.txt")));
            Assert.True(File.Exists(Path.Combine(_targetDir, "file2_copy.txt")));
            Assert.True(Directory.Exists(Path.Combine(_targetDir, "EmptySubDir_copy")));
            Assert.True(progressReports.Count > 0, "Expected progress reports");
            Assert.Contains(progressReports, p => p.Percentage == 100);
        }

        private static async Task CreateDummyFile(string path, long size)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            fs.SetLength(size);
            await fs.FlushAsync();
        }
    }
}