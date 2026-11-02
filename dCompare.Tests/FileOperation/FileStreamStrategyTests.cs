using Xunit;
using dCompare.FileOperation.Strategies;
using System;
using System.IO;
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
    }

}