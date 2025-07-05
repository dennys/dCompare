using dCompare.FileOperation.Strategies;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.FileOperation
{
    public class Kernel32FileCopyStrategyTests : IDisposable
    {
        private readonly string _testDir;
        private readonly Kernel32FileCopyStrategy _strategy;

        public Kernel32FileCopyStrategyTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
            _strategy = new Kernel32FileCopyStrategy();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Fact]
        public async Task CopyFileAsync_CopiesFileSuccessfully()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDir, "source.txt");
            string destFile = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(sourceFile, "Test content");

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, true, null, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destFile));
            Assert.Equal(File.ReadAllText(sourceFile), File.ReadAllText(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_CreatesDestinationDirectory()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDir, "source.txt");
            string destDir = Path.Combine(_testDir, "subdir");
            string destFile = Path.Combine(destDir, "dest.txt");
            File.WriteAllText(sourceFile, "Test content");

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, true, null, CancellationToken.None);

            // Assert
            Assert.True(Directory.Exists(destDir));
            Assert.True(File.Exists(destFile));
        }

        [Fact]
        public async Task CopyFileAsync_RespectsCancellation()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDir, "source.txt");
            string destFile = Path.Combine(_testDir, "dest.txt");
            File.WriteAllBytes(sourceFile, new byte[1024 * 1024]); // 1MB file
            using var cts = new CancellationTokenSource();

            // Act
            var copyTask = _strategy.CopyFileAsync(sourceFile, destFile, true, null, cts.Token);
            cts.Cancel();

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => copyTask);
        }

        [Fact]
        public async Task CopyFileAsync_ReportsProgress()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDir, "source.txt");
            string destFile = Path.Combine(_testDir, "dest.txt");
            File.WriteAllBytes(sourceFile, new byte[1024 * 1024]); // 1MB file
            long totalBytes = 0;
            long transferredBytes = 0;

            // Act
            await _strategy.CopyFileAsync(sourceFile, destFile, true,
                (total, transferred, _, _, _, _, _, _, _) =>
                {
                    totalBytes = total;
                    transferredBytes = transferred;
                    return CopyProgressResult.PROGRESS_CONTINUE;
                },
                CancellationToken.None);

            // Assert
            Assert.True(totalBytes > 0);
            Assert.Equal(totalBytes, transferredBytes);
        }

        [Fact]
        public void MoveFile_MovesFileSuccessfully()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDir, "source.txt");
            string destFile = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(sourceFile, "Test content");

            // Act
            _strategy.MoveFile(sourceFile, destFile, true);

            // Assert
            Assert.False(File.Exists(sourceFile));
            Assert.True(File.Exists(destFile));
            Assert.Equal("Test content", File.ReadAllText(destFile));
        }

        [Fact]
        public void DeleteFile_DeletesExistingFile()
        {
            // Arrange
            string filePath = Path.Combine(_testDir, "test.txt");
            File.WriteAllText(filePath, "Test content");

            // Act
            _strategy.DeleteFile(filePath);

            // Assert
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public void DeleteDirectory_DeletesExistingDirectory()
        {
            // Arrange
            string subDir = Path.Combine(_testDir, "subdir");
            Directory.CreateDirectory(subDir);

            // Act
            _strategy.DeleteDirectory(subDir);

            // Assert
            Assert.False(Directory.Exists(subDir));
        }

        [Fact]
        public async Task CopyFileAsync_ThrowsWhenSourceDoesNotExist()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDir, "nonexistent.txt");
            string destFile = Path.Combine(_testDir, "dest.txt");

            // Act & Assert
            await Assert.ThrowsAsync<Win32Exception>(() =>
                _strategy.CopyFileAsync(sourceFile, destFile, true, null, CancellationToken.None));
        }

        [Fact]
        public async Task CopyFileAsync_RespectsOverwriteFlag()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDir, "source.txt");
            string destFile = Path.Combine(_testDir, "dest.txt");
            File.WriteAllText(sourceFile, "Source content");
            File.WriteAllText(destFile, "Existing content");

            // Act & Assert
            await Assert.ThrowsAsync<Win32Exception>(() =>
                _strategy.CopyFileAsync(sourceFile, destFile, false, null, CancellationToken.None));
        }
    }
}