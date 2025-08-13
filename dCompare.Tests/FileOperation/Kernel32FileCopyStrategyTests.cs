using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using Xunit;
using dCompare.FileOperation.Strategies;

namespace dCompare.FileOperation.Tests
{
    public class Kernel32FileCopyStrategyTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly Kernel32FileCopyStrategy _strategy;

        public Kernel32FileCopyStrategyTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "Kernel32Tests_" + Guid.NewGuid().ToString()[..8]);
            Directory.CreateDirectory(_testDirectory);
            _strategy = new Kernel32FileCopyStrategy();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        #region CopyFileAsync Tests

        [Fact]
        public async Task CopyFileAsync_ValidSourceAndDestination_ShouldCopyFile()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", "Test content");
            var destFile = Path.Combine(_testDirectory, "dest.txt");

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, null, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal("Test content", File.ReadAllText(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_WithProgressCallback_ShouldInvokeCallback()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", new string('A', 1000));
            var destFile = Path.Combine(_testDirectory, "dest.txt");
            var callbackInvoked = false;
            long totalBytes = 0;
            long transferredBytes = 0;

            dCompare.FileOperation.Strategies.CopyProgressCallback progressCallback = (total, transferred, _, _, _, _, _, _, _) =>
            {
                callbackInvoked = true;
                totalBytes = total;
                transferredBytes = transferred;
                return CopyProgressResult.PROGRESS_CONTINUE;
            };

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, progressCallback, CancellationToken.None);

            // Assert
            Assert.True(callbackInvoked);
            Assert.True(totalBytes > 0);
            Assert.True(transferredBytes > 0);
        }

        [Fact]
        public async Task CopyFileAsync_WithCancellation_ShouldThrowWin32Exception()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", new string('A', 10000));
            var destFile = Path.Combine(_testDirectory, "dest.txt");
            var cts = new CancellationTokenSource();

            dCompare.FileOperation.Strategies.CopyProgressCallback progressCallback = (_, _, _, _, _, _, _, _, _) =>
            {
                cts.Cancel(); // Cancel during progress
                return CopyProgressResult.PROGRESS_CANCEL;
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Win32Exception>(
                async () => await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, progressCallback, cts.Token));

            // Verify it's a cancellation-related Win32Exception
            // The error message should indicate the operation was aborted
            Assert.Contains("¤¤¤î", exception.Message); // Contains "aborted" in Chinese
        }

        [Fact]
        public async Task CopyFileAsync_OverwriteFalse_ExistingDestination_ShouldThrowWin32Exception()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", "Source content");
            var destFile = CreateTestFile("dest.txt", "Existing content");

            // Act & Assert
            await Assert.ThrowsAsync<Win32Exception>(
                async () => await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, null, CancellationToken.None));
        }

        [Fact]
        public async Task CopyFileAsync_OverwriteTrue_ExistingDestination_ShouldOverwriteFile()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", "New content");
            var destFile = CreateTestFile("dest.txt", "Old content");

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: true, null, CancellationToken.None);

            // Assert
            Assert.Equal("New content", File.ReadAllText(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_NonExistentSourceFile_ShouldThrowWin32Exception()
        {
            // Arrange
            var sourceFile = Path.Combine(_testDirectory, "nonexistent.txt");
            var destFile = Path.Combine(_testDirectory, "dest.txt");

            // Act & Assert
            await Assert.ThrowsAsync<Win32Exception>(
                async () => await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, null, CancellationToken.None));
        }

        [Fact]
        public async Task CopyFileAsync_DestinationDirectoryDoesNotExist_ShouldCreateDirectory()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", "Test content");
            var destDir = Path.Combine(_testDirectory, "newdir", "subdir");
            var destFile = Path.Combine(destDir, "dest.txt");

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, null, CancellationToken.None);

            // Assert
            Assert.True(Directory.Exists(destDir));
            Assert.True(File.Exists(destFile));
            Assert.Equal("Test content", File.ReadAllText(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_EmptyFile_ShouldCopySuccessfully()
        {
            // Arrange
            var sourceFile = CreateTestFile("empty.txt", "");
            var destFile = Path.Combine(_testDirectory, "dest.txt");

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, null, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Empty(File.ReadAllText(destFile));
        }

        [Theory]
        [InlineData("file with spaces.txt")]
        [InlineData("file-with-dashes.txt")]
        [InlineData("file_with_underscores.txt")]
        [InlineData("file.with.dots.txt")]
        public async Task CopyFileAsync_VariousFileNames_ShouldCopySuccessfully(string fileName)
        {
            // Arrange
            var sourceFile = CreateTestFile(fileName, "Test content");
            var destFile = Path.Combine(_testDirectory, "dest_" + fileName);

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, null, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal("Test content", File.ReadAllText(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_LargeFile_ShouldCopySuccessfully()
        {
            // Arrange
            var largeContent = new string('X', 1024 * 1024); // 1MB
            var sourceFile = CreateTestFile("large.txt", largeContent);
            var destFile = Path.Combine(_testDirectory, "dest_large.txt");

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, overwrite: false, null, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal(largeContent.Length, new FileInfo(destFile).Length);
        }

        #endregion

        #region MoveFile Tests

        [Fact]
        public void MoveFile_ValidSourceAndDestination_ShouldMoveFile()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", "Test content");
            var destFile = Path.Combine(_testDirectory, "moved.txt");

            // Act
            _strategy.MoveFile(sourceFile, destFile, overwrite: false);

            // Assert
            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.Equal("Test content", File.ReadAllText(destFile));
        }

        [Fact]
        public void MoveFile_OverwriteTrue_ExistingDestination_ShouldOverwriteFile()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", "New content");
            var destFile = CreateTestFile("dest.txt", "Old content");

            // Act
            _strategy.MoveFile(sourceFile, destFile, overwrite: true);

            // Assert
            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.Equal("New content", File.ReadAllText(destFile));
        }

        [Fact]
        public void MoveFile_OverwriteFalse_ExistingDestination_ShouldThrowIOException()
        {
            // Arrange
            var sourceFile = CreateTestFile("source.txt", "Source content");
            var destFile = CreateTestFile("dest.txt", "Existing content");

            // Act & Assert
            Assert.Throws<IOException>(() => _strategy.MoveFile(sourceFile, destFile, overwrite: false));
        }

        [Fact]
        public void MoveFile_NonExistentSourceFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var sourceFile = Path.Combine(_testDirectory, "nonexistent.txt");
            var destFile = Path.Combine(_testDirectory, "dest.txt");

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => _strategy.MoveFile(sourceFile, destFile, overwrite: false));
        }

        #endregion

        #region DeleteFile Tests

        [Fact]
        public void DeleteFile_ExistingFile_ShouldDeleteFile()
        {
            // Arrange
            var testFile = CreateTestFile("test.txt", "Test content");
            Assert.True(File.Exists(testFile)); // Precondition

            // Act
            _strategy.DeleteFile(testFile);

            // Assert
            Assert.False(File.Exists(testFile));
        }

        [Fact]
        public void DeleteFile_NonExistentFile_ShouldNotThrow()
        {
            // Arrange
            var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.txt");

            // Act & Assert - should not throw
            _strategy.DeleteFile(nonExistentFile);
        }

        [Fact]
        public void DeleteFile_ReadOnlyFile_ShouldThrowUnauthorizedAccessException()
        {
            // Arrange
            var testFile = CreateTestFile("readonly.txt", "Test content");
            File.SetAttributes(testFile, FileAttributes.ReadOnly);

            // Act & Assert
            Assert.Throws<UnauthorizedAccessException>(() => _strategy.DeleteFile(testFile));

            // Cleanup - remove readonly attribute for disposal
            File.SetAttributes(testFile, FileAttributes.Normal);
        }

        #endregion

        #region DeleteDirectory Tests

        [Fact]
        public void DeleteDirectory_ExistingEmptyDirectory_ShouldDeleteDirectory()
        {
            // Arrange
            var testDir = Path.Combine(_testDirectory, "emptydir");
            Directory.CreateDirectory(testDir);
            Assert.True(Directory.Exists(testDir)); // Precondition

            // Act
            _strategy.DeleteDirectory(testDir);

            // Assert
            Assert.False(Directory.Exists(testDir));
        }

        [Fact]
        public void DeleteDirectory_NonExistentDirectory_ShouldThrowDirectoryNotFoundException()
        {
            // Arrange
            var nonExistentDir = Path.Combine(_testDirectory, "nonexistentdir");

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() => _strategy.DeleteDirectory(nonExistentDir));
        }

        [Fact]
        public void DeleteDirectory_DirectoryWithFiles_ShouldThrowIOException()
        {
            // Arrange
            var testDir = Path.Combine(_testDirectory, "dirwithfiles");
            Directory.CreateDirectory(testDir);
            CreateTestFile(Path.Combine(testDir, "file.txt"), "content");

            // Act & Assert
            Assert.Throws<IOException>(() => _strategy.DeleteDirectory(testDir));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task IntegrationTest_CopyMoveDelete_ShouldWorkTogether()
        {
            // Arrange
            var sourceFile = CreateTestFile("integration.txt", "Integration test content");
            var copyDestFile = Path.Combine(_testDirectory, "copy_dest.txt");
            var moveDestFile = Path.Combine(_testDirectory, "move_dest.txt");

            // Act - Copy
            await _strategy.CopyFileAsync(sourceFile, copyDestFile, overwrite: false, null, CancellationToken.None);

            // Act - Move
            _strategy.MoveFile(copyDestFile, moveDestFile, overwrite: false);

            // Act - Delete original
            _strategy.DeleteFile(sourceFile);

            // Assert
            Assert.False(File.Exists(sourceFile));
            Assert.False(File.Exists(copyDestFile));
            Assert.True(File.Exists(moveDestFile));
            Assert.Equal("Integration test content", File.ReadAllText(moveDestFile));

            // Cleanup
            _strategy.DeleteFile(moveDestFile);
            Assert.False(File.Exists(moveDestFile));
        }

        #endregion

        #region Helper Methods

        private string CreateTestFile(string fileName, string content)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(filePath, content);
            return filePath;
        }

        #endregion
    }

    #region Supporting Types (if not already defined in the main project)

    // These would typically be defined in your main project
    // Remove this section if these types are already available in your project
    public interface IFileOperationStrategy
    {
        Task CopyFileAsync(string sourceFile, string destFile, bool overwrite, dCompare.FileOperation.Strategies.CopyProgressCallback? progressCallback, CancellationToken cancellationToken);
        void MoveFile(string sourceFile, string destFile, bool overwrite);
        void DeleteFile(string filePath);
        void DeleteDirectory(string dirPath);
    }

    #endregion
}