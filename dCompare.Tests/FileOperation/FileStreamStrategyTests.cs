using Xunit;
using dCompare.FileOperation.Strategies;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;


namespace dCompare.Tests.FileOperation
{

    public class FileStreamStrategyTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _sourceFile;
        private readonly string _destFile;

        public FileStreamStrategyTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "FileStreamStrategyTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_testRoot);
            _sourceFile = Path.Combine(_testRoot, "source.txt");
            _destFile = Path.Combine(_testRoot, "dest.txt");
            File.WriteAllText(_sourceFile, "This is a test file.");
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                // Recursive delete cannot remove read-only files (the attribute tests leave some).
                foreach (var file in Directory.GetFiles(_testRoot, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(_testRoot, true);
            }
        }

        [Fact]
        public async Task CopyFileAsync_CopiesFileCorrectly()
        {
            // Arrange
            var strategy = new FileStreamStrategy();

            // Act
            await strategy.CopyFileAsync(_sourceFile, _destFile, false, null, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(_destFile));
            Assert.Equal(File.ReadAllText(_sourceFile), File.ReadAllText(_destFile));
        }

        [Fact]
        public async Task CopyFileAsync_OverwriteFalse_ThrowsWhenDestExists()
        {
            // Arrange
            var strategy = new FileStreamStrategy();
            File.WriteAllText(_destFile, "existing content");

            // Act & Assert
            await Assert.ThrowsAsync<IOException>(() =>
                strategy.CopyFileAsync(_sourceFile, _destFile, false, null, CancellationToken.None));
        }

        [Fact]
        public async Task CopyFileAsync_OverwriteTrue_OverwritesDest()
        {
            // Arrange
            var strategy = new FileStreamStrategy();
            File.WriteAllText(_destFile, "existing content");

            // Act
            await strategy.CopyFileAsync(_sourceFile, _destFile, true, null, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(_destFile));
            Assert.Equal(File.ReadAllText(_sourceFile), File.ReadAllText(_destFile));
        }

        // Copy fills <dest>.dCompare.tmp and swaps it in; a leftover temp file means the swap or the
        // failure cleanup was skipped, and the user's previous version must survive a refused overwrite.
        [Fact]
        public async Task CopyFileAsync_SuccessAndFailedCopies_LeaveNoTempFileBehind()
        {
            // Arrange
            var strategy = new FileStreamStrategy();
            var swappedDest = Path.Combine(_testRoot, "swap.txt");
            File.WriteAllText(swappedDest, "old content");
            var existingDest = Path.Combine(_testRoot, "existing.txt");
            File.WriteAllText(existingDest, "existing content");
            var missingSource = Path.Combine(_testRoot, "missing.txt");

            // Act - successful overwrite, which is the case that goes through the temp file
            bool sawTempDuringCopy = false;
            CopyProgressCallback watchTemp = (long _, long _, long _, long _, uint _, CopyProgressCallbackReason _, IntPtr _, IntPtr _, IntPtr _) =>
            {
                sawTempDuringCopy = File.Exists(swappedDest + ".dCompare.tmp");
                return CopyProgressResult.PROGRESS_CONTINUE;
            };
            await strategy.CopyFileAsync(_sourceFile, swappedDest, true, watchTemp, CancellationToken.None);

            // Assert
            Assert.Equal(File.ReadAllText(_sourceFile), File.ReadAllText(swappedDest));
            Assert.True(sawTempDuringCopy, "copy did not stage the new version in <dest>.dCompare.tmp");
            AssertNoTempFiles();

            // Act & Assert - overwrite refused onto an existing destination
            await Assert.ThrowsAsync<IOException>(() =>
                strategy.CopyFileAsync(_sourceFile, existingDest, false, null, CancellationToken.None));
            Assert.Equal("existing content", File.ReadAllText(existingDest));
            AssertNoTempFiles();

            // Act & Assert - missing source
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                strategy.CopyFileAsync(missingSource, Path.Combine(_testRoot, "never.txt"), false, null, CancellationToken.None));
            AssertNoTempFiles();
        }

        private void AssertNoTempFiles()
        {
            string[] leftovers = Directory.GetFiles(_testRoot, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".dCompare.tmp", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.Empty(leftovers);
        }

        [Fact]
        public void MoveFile_MovesFile()
        {
            // Arrange
            var strategy = new FileStreamStrategy();
            var moveDest = Path.Combine(_testRoot, "moved.txt");

            // Act
            strategy.MoveFile(_sourceFile, moveDest, false);

            // Assert
            Assert.False(File.Exists(_sourceFile));
            Assert.True(File.Exists(moveDest));
        }

        [Fact]
        public void DeleteFile_DeletesTheFile()
        {
            // Arrange
            var strategy = new FileStreamStrategy();

            // Act
            strategy.DeleteFile(_sourceFile);

            // Assert
            Assert.False(File.Exists(_sourceFile));
        }

        [Fact]
        public void DeleteDirectory_DeletesTheDirectory()
        {
            // Arrange
            var strategy = new FileStreamStrategy();
            var dirToDelete = Path.Combine(_testRoot, "deleteme");
            Directory.CreateDirectory(dirToDelete);

            // Act
            strategy.DeleteDirectory(dirToDelete);

            // Assert
            Assert.False(Directory.Exists(dirToDelete));
        }

        [Fact]
        public async Task CopyFileAsync_ReadOnlySource_DestinationKeepsReadOnlyAttribute()
        {
            // Arrange
            File.SetAttributes(_sourceFile, FileAttributes.ReadOnly);
            var strategy = new FileStreamStrategy();

            // Act
            await strategy.CopyFileAsync(_sourceFile, _destFile, false, null, CancellationToken.None);

            // Assert
            Assert.True((File.GetAttributes(_destFile) & FileAttributes.ReadOnly) != 0);
        }

        [Fact]
        public async Task CopyFileAsync_SecondCopyOverReadOnlyDestination_Succeeds()
        {
            // Arrange: first copy makes the destination read-only (source is read-only);
            // a second copy to the same path must not fail with ERROR_ACCESS_DENIED.
            File.SetAttributes(_sourceFile, FileAttributes.ReadOnly);
            var strategy = new FileStreamStrategy();
            await strategy.CopyFileAsync(_sourceFile, _destFile, false, null, CancellationToken.None);
            Assert.True((File.GetAttributes(_destFile) & FileAttributes.ReadOnly) != 0);

            string secondSource = Path.Combine(_testRoot, "second.txt");
            File.WriteAllText(secondSource, "second content");

            // Act & Assert
            await strategy.CopyFileAsync(secondSource, _destFile, true, null, CancellationToken.None);
            Assert.Equal("second content", File.ReadAllText(_destFile));
        }
    }

}