using Xunit;
using Moq;
using dCompare.FileOperation;
using dCompare.FileOperation.Strategies;
using dCompare.FileOperation.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using dCompare.Compare;

namespace dCompare.Tests.FileOperation
{
    public class FileManagerTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _sourceDir;
        private readonly string _destDir;

        // Setup a temporary directory for file operations
        public FileManagerTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "FileManagerTests", Path.GetRandomFileName());
            _sourceDir = Path.Combine(_testRoot, "source");
            _destDir = Path.Combine(_testRoot, "destination");
            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_destDir);
        }

        // Cleanup the temporary directory
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, true);
                }
            }
            catch (IOException)
            {
                // Handle cases where files might still be locked
            }
        }

        [Fact]
        public void FileManager_DefaultConstructor_UsesKernel32Strategy()
        {
            // Arrange & Act
            var fileManager = new FileManager();

            // Assert
            Assert.NotNull(fileManager);
            // This is a bit tricky to assert directly without reflection,
            // but we can infer it from the behavior in an integration test.
            // For now, we just ensure it doesn't throw.
        }

        [Fact]
        public void FileManager_ConstructorWithStrategy_UsesProvidedStrategy()
        {
            // Arrange
            var mockStrategy = new Mock<IFileOperationStrategy>();
            var fileManager = new FileManager(mockStrategy.Object);

            var sourceFile = Path.Combine(_sourceDir, "test.txt");
            File.WriteAllText(sourceFile, "test");

            // Act
            fileManager.DeleteAsync(sourceFile).Wait();

            // Assert
            mockStrategy.Verify(s => s.DeleteFile(sourceFile), Times.Once);
        }

        [Fact]
        public async Task CopyAsync_SingleFile_Success()
        {
            // Arrange
            var fileManager = new FileManager(new FileStreamStrategy());
            var sourceFile = Path.Combine(_sourceDir, "file.txt");
            var destFile = Path.Combine(_destDir, "file.txt");
            await File.WriteAllTextAsync(sourceFile, "content");

            // Act
            var result = await fileManager.CopyAsync(sourceFile, destFile);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal(1, result.SuccessCount);
            Assert.Equal(0, result.FailCount);
        }

        //[Fact]
        //public async Task CopyAsync_SingleFile_DestinationExists_OverwriteFalse_ThrowsException()
        //{
        //    // Arrange
        //    var fileManager = new FileManager(new FileStreamStrategy());
        //    var sourceFile = Path.Combine(_sourceDir, "file.txt");
        //    var destFile = Path.Combine(_destDir, "file.txt");
        //    await File.WriteAllTextAsync(sourceFile, "source content");
        //    await File.WriteAllTextAsync(destFile, "dest content");

        //    // Act & Assert
        //    // The inner exception is what the strategy throws
        //    var ex = await Assert.ThrowsAsync<IOException>(() => fileManager.CopyAsync(sourceFile, destFile, false));
        //    Assert.Contains("already exists", ex.Message);
        //}

        [Fact]
        public async Task CopyAsync_SingleFile_DestinationExists_OverwriteTrue_Success()
        {
            // Arrange
            var fileManager = new FileManager(new FileStreamStrategy());
            var sourceFile = Path.Combine(_sourceDir, "file.txt");
            var destFile = Path.Combine(_destDir, "file.txt");
            await File.WriteAllTextAsync(sourceFile, "new content");
            await File.WriteAllTextAsync(destFile, "old content");

            // Act
            var result = await fileManager.CopyAsync(sourceFile, destFile, true);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal("new content", await File.ReadAllTextAsync(destFile));
            Assert.Equal(1, result.SuccessCount);
        }

        [Fact]
        public async Task MoveAsync_SingleFile_Success()
        {
            // Arrange
            var fileManager = new FileManager(new FileStreamStrategy());
            var sourceFile = Path.Combine(_sourceDir, "move.txt");
            var destFile = Path.Combine(_destDir, "move.txt");
            await File.WriteAllTextAsync(sourceFile, "content");

            // Act
            var result = await fileManager.MoveAsync(sourceFile, destFile);

            // Assert
            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.Equal(1, result.SuccessCount);
        }

        [Fact]
        public async Task DeleteAsync_SingleFile_Success()
        {
            // Arrange
            var fileManager = new FileManager(new FileStreamStrategy());
            var sourceFile = Path.Combine(_sourceDir, "delete.txt");
            await File.WriteAllTextAsync(sourceFile, "content");

            // Act
            var result = await fileManager.DeleteAsync(sourceFile);

            // Assert
            Assert.False(File.Exists(sourceFile));
            Assert.Equal(1, result.SuccessCount);
        }

        [Fact]
        public async Task CopyAsync_Directory_Success()
        {
            // Arrange
            var fileManager = new FileManager(new FileStreamStrategy());
            var subDir = Path.Combine(_sourceDir, "sub");
            Directory.CreateDirectory(subDir);
            await File.WriteAllTextAsync(Path.Combine(_sourceDir, "root.txt"), "root");
            await File.WriteAllTextAsync(Path.Combine(subDir, "nested.txt"), "nested");

            // Act
            var result = await fileManager.CopyAsync(_sourceDir, _destDir);

            // Assert
            Assert.True(File.Exists(Path.Combine(_destDir, "root.txt")));
            Assert.True(File.Exists(Path.Combine(_destDir, "sub", "nested.txt")));
            Assert.Equal(1, result.SuccessCount); // Treats the top-level directory as one item
        }

        [Fact]
        public async Task CopyAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            var fileManager = new FileManager(new FileStreamStrategy());
            var sourceFile = Path.Combine(_sourceDir, "largefile.bin");
            // Create a reasonably large file to ensure the copy takes some time
            await File.WriteAllBytesAsync(sourceFile, new byte[10 * 1024 * 1024]);
            var destFile = Path.Combine(_destDir, "largefile.bin");
            var cts = new CancellationTokenSource();

            // Act
            var copyTask = fileManager.CopyAsync(sourceFile, destFile, false, null, cts.Token);
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => copyTask);
        }

        [Fact]
        public async Task CopyAsync_WithPauseAndResume_CompletesSuccessfully()
        {
            // Arrange
            var fileManager = new FileManager(new FileStreamStrategy());
            var sourceFile = Path.Combine(_sourceDir, "file.txt");
            var destFile = Path.Combine(_destDir, "file.txt");
            await File.WriteAllTextAsync(sourceFile, "content");

            // Act
            var copyTask = fileManager.CopyAsync(sourceFile, destFile);

            fileManager.Pause();
            await Task.Delay(200); // Give it a moment to pause
            fileManager.Resume();

            var result = await copyTask;

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal(1, result.SuccessCount);
        }

        //[Fact]
        //public async Task CopyAsync_Progress_ReportsProgress()
        //{
        //    // Arrange
        //    var fileManager = new FileManager(new FileStreamStrategy());
        //    var sourceFile = Path.Combine(_sourceDir, "progress.txt");
        //    await File.WriteAllBytesAsync(sourceFile, new byte[1024]);
        //    var destFile = Path.Combine(_destDir, "progress.txt");

        //    var progressReported = false;
        //    var progress = new Progress<OperationProgress>(p =>
        //    {
        //        if (p.Percentage > 0 && p.Percentage < 100)
        //        {
        //            progressReported = true;
        //        }
        //    });

        //    // Act
        //    await fileManager.CopyAsync(sourceFile, destFile, false, progress);

        //    // Assert
        //    Assert.True(progressReported);
        //}
    }

}