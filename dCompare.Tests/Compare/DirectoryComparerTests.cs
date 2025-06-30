using dCompare.Compare;
using dCompare.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests
{
    public class DirectoryComparerTests : IDisposable
    {
        private readonly Mock<IFileSystem> _fileSystemMock;
        private readonly Mock<IHashProvider> _hashProviderMock;
        private readonly DirectoryComparer _comparer;
        private readonly string _tempDir;

        public DirectoryComparerTests()
        {
            _fileSystemMock = new Mock<IFileSystem>();
            _hashProviderMock = new Mock<IHashProvider>();
            _comparer = new DirectoryComparer(_fileSystemMock.Object, _hashProviderMock.Object);

            // Create a temporary directory for test files
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            // Clean up temporary directory
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact]
        public async Task CompareAsync_IdenticalDirectories_ReturnsIdenticalStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            _fileSystemMock.Setup(fs => fs.DirectoryExists(leftPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetDirectories(It.IsAny<string>())).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.GetFiles(It.IsAny<string>())).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.GetLastWriteTime(It.IsAny<string>())).Returns(DateTime.Now);

            // Act
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize, false);

            // Assert
            Assert.Single(result);
            Assert.Equal(ComparisonStatus.Identical, result[0].Status);
        }

        [Fact]
        public async Task CompareAsync_DifferentFiles_ReturnsModifiedStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string fileName = "test.txt";
            string leftFilePath = Path.Combine(leftPath, fileName);
            string rightFilePath = Path.Combine(rightPath, fileName);

            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);
            File.WriteAllBytes(leftFilePath, new byte[100]); // 100 bytes
            File.WriteAllBytes(rightFilePath, new byte[200]); // 200 bytes
            File.SetLastWriteTime(rightFilePath, DateTime.Now.AddSeconds(2));

            _fileSystemMock.Setup(fs => fs.DirectoryExists(leftPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetDirectories(It.IsAny<string>())).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.GetFiles(leftPath)).Returns(new[] { leftFilePath });
            _fileSystemMock.Setup(fs => fs.GetFiles(rightPath)).Returns(new[] { rightFilePath });
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetFileInfo(leftFilePath)).Returns(new FileInfo(leftFilePath));
            _fileSystemMock.Setup(fs => fs.GetFileInfo(rightFilePath)).Returns(new FileInfo(rightFilePath));
            _fileSystemMock.Setup(fs => fs.GetLastWriteTime(It.IsAny<string>())).Returns(DateTime.Now);

            // Act
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize, false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.Modified, result[0].Status);
            Assert.Equal(ComparisonStatus.Modified, result[1].Status);
            Assert.Equal("📄 " + fileName, result[1].TreeIndent);
        }

        [Fact]
        public async Task CompareAsync_OneSidedFile_ReturnsLeftOnlyStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string fileName = "test.txt";
            string leftFilePath = Path.Combine(leftPath, fileName);

            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);
            File.WriteAllBytes(leftFilePath, new byte[100]); // 100 bytes

            _fileSystemMock.Setup(fs => fs.DirectoryExists(leftPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetDirectories(It.IsAny<string>())).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.GetFiles(leftPath)).Returns(new[] { leftFilePath });
            _fileSystemMock.Setup(fs => fs.GetFiles(rightPath)).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.FileExists(leftFilePath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.FileExists(Path.Combine(rightPath, fileName))).Returns(false);
            _fileSystemMock.Setup(fs => fs.GetFileInfo(leftFilePath)).Returns(new FileInfo(leftFilePath));
            _fileSystemMock.Setup(fs => fs.GetLastWriteTime(It.IsAny<string>())).Returns(DateTime.Now);

            // Act
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize, false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.Modified, result[0].Status);
            Assert.Equal(ComparisonStatus.LeftOnly, result[1].Status);
            Assert.Equal("📄 " + fileName, result[1].TreeIndent);
        }

        [Fact]
        public async Task CompareAsync_MD5Comparison_DifferentContent_ReturnsModifiedStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string fileName = "test.txt";
            string leftFilePath = Path.Combine(leftPath, fileName);
            string rightFilePath = Path.Combine(rightPath, fileName);

            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);
            File.WriteAllBytes(leftFilePath, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(rightFilePath, new byte[] { 4, 5, 6 });

            _fileSystemMock.Setup(fs => fs.DirectoryExists(leftPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetDirectories(It.IsAny<string>())).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.GetFiles(leftPath)).Returns(new[] { leftFilePath });
            _fileSystemMock.Setup(fs => fs.GetFiles(rightPath)).Returns(new[] { rightFilePath });
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetFileInfo(leftFilePath)).Returns(new FileInfo(leftFilePath));
            _fileSystemMock.Setup(fs => fs.GetFileInfo(rightFilePath)).Returns(new FileInfo(rightFilePath));
            _fileSystemMock.Setup(fs => fs.OpenRead(leftFilePath)).Returns(new MemoryStream(new byte[] { 1, 2, 3 }));
            _fileSystemMock.Setup(fs => fs.OpenRead(rightFilePath)).Returns(new MemoryStream(new byte[] { 4, 5, 6 }));
            _hashProviderMock.Setup(h => h.ComputeMD5(It.IsAny<Stream>(), 1.0)).Returns<Stream, double>((s, _) =>
            {
                s.Position = 0;
                return s.ReadByte() == 1 ? new byte[] { 1 } : new byte[] { 2 };
            });

            // Act
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.MD5, false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.Modified, result[0].Status);
            Assert.Equal(ComparisonStatus.Modified, result[1].Status);
        }

        [Fact]
        public async Task CompareAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _comparer.CompareAsync(leftPath, rightPath, cts.Token, null, ComparisonAlgorithm.ModifiedDateAndSize, false));
        }

        [Fact]
        public void CompareSubtree_FileConflict_ReturnsConflictStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string subPath = "test";
            string leftFilePath = Path.Combine(leftPath, subPath);
            string rightDirPath = Path.Combine(rightPath, subPath);

            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);
            Directory.CreateDirectory(rightDirPath);
            File.WriteAllBytes(leftFilePath, new byte[100]);

            _fileSystemMock.Setup(fs => fs.FileExists(leftFilePath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightDirPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetFileInfo(leftFilePath)).Returns(new FileInfo(leftFilePath));

            // Act
            var result = _comparer.CompareSubtree(leftPath, rightPath, subPath, CancellationToken.None, ComparisonAlgorithm.ModifiedDateAndSize);

            // Assert
            Assert.Single(result);
            Assert.Equal(ComparisonStatus.Conflict, result[0].Status);
            Assert.Equal("📁 test", result[0].TreeIndent);
        }

        // New test cases to improve coverage

        [Fact]
        public void Compare_SynchronousCall_ReturnsIdenticalStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            var fileSystem = new FileSystem();
            var hashProvider = new HashProvider();
            var comparer = new DirectoryComparer(fileSystem, hashProvider);

            // Act
            var result = comparer.Compare(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize, false);

            // Assert
            Assert.Single(result);
            Assert.Equal(ComparisonStatus.Identical, result[0].Status);
        }

        [Fact]
        public async Task CompareAsync_ParallelProcessing_ReturnsIdenticalStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            var fileSystem = new FileSystem();
            var hashProvider = new HashProvider();
            var comparer = new DirectoryComparer(fileSystem, hashProvider);

            // Act
            var result = await comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize, true);

            // Assert
            Assert.Single(result);
            Assert.Equal(ComparisonStatus.Identical, result[0].Status);
        }

        [Fact]
        public async Task CompareAsync_WithProgressCallback_InvokesCallback()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string fileName = "test.txt";
            string leftFilePath = Path.Combine(leftPath, fileName);
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);
            File.WriteAllBytes(leftFilePath, new byte[100]);

            var fileSystem = new FileSystem();
            var hashProvider = new HashProvider();
            var comparer = new DirectoryComparer(fileSystem, hashProvider);
            double progress = 0;
            var progressCallback = new Action<double>(p => progress = p);

            // Act
            var result = await comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, progressCallback, ComparisonAlgorithm.ModifiedDateAndSize, false);

            // Assert
            Assert.True(progress > 0, "Progress callback was not invoked.");
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.LeftOnly, result[1].Status);
        }

        //[Fact]
        //public async Task CompareAsync_NonExistentPath_ThrowsDirectoryNotFoundException()
        //{
        //    // Arrange
        //    string invalidPath = Path.Combine(_tempDir, "invalid");
        //    string rightPath = Path.Combine(_tempDir, "right");
        //    Directory.CreateDirectory(rightPath);

        //    var fileSystem = new FileSystem();
        //    var hashProvider = new HashProvider();
        //    var comparer = new DirectoryComparer(fileSystem, hashProvider);

        //    // Act & Assert
        //    await Assert.ThrowsAsync<DirectoryNotFoundException>(
        //        () => comparer.CompareAsync(invalidPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize, false));
        //}

        [Fact]
        public async Task CompareAsync_NestedDirectories_ReturnsIdenticalStatus()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string subDir = "sub";
            string leftSubPath = Path.Combine(leftPath, subDir);
            string rightSubPath = Path.Combine(rightPath, subDir);

            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);
            Directory.CreateDirectory(leftSubPath);
            Directory.CreateDirectory(rightSubPath);

            var fileSystem = new FileSystem();
            var hashProvider = new HashProvider();
            var comparer = new DirectoryComparer(fileSystem, hashProvider);

            // Act
            var result = await comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize, false);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.Identical, result[0].Status);
            Assert.Equal(ComparisonStatus.Identical, result[1].Status);
        }

        [Fact]
        public void CompareSubtree_NonExistentPaths_ReturnsEmpty()
        {
            // Arrange
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string subPath = "invalid";

            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

            // Act
            var result = _comparer.CompareSubtree(leftPath, rightPath, subPath, CancellationToken.None, ComparisonAlgorithm.ModifiedDateAndSize);

            // Assert
            Assert.Empty(result);
        }
    }
}