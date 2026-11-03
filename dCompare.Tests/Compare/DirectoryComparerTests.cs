using dCompare.Compare;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.DirTreeSystem;
using dCompare.DirTreeSystem.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.Compare
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
            _comparer = new DirectoryComparer(null, _fileSystemMock.Object, _hashProviderMock.Object);

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
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);

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
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);

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
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);

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
            _hashProviderMock.Setup(h => h.ComputeMD5(It.IsAny<Stream>(), 1.0, It.IsAny<CancellationToken>())).Returns<Stream, double, CancellationToken>((s, _, _) =>
            {
                s.Position = 0;
                return s.ReadByte() == 1 ? new byte[] { 1 } : new byte[] { 2 };
            });

            // Act
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.MD5);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.Modified, result[0].Status);
            Assert.Equal(ComparisonStatus.Modified, result[1].Status);
        }

        //[Fact]
        //public async Task CompareAsync_Cancellation_ThrowsOperationCanceledException()
        //{
        //    // Arrange
        //    string leftPath = Path.Combine(_tempDir, "left");
        //    string rightPath = Path.Combine(_tempDir, "right");
        //    var cts = new CancellationTokenSource();
        //    cts.Cancel();

        //    _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        //    // Act & Assert
        //    await Assert.ThrowsAsync<OperationCanceledException>(
        //        () => _comparer.CompareAsync(leftPath, rightPath, cts.Token, null, ComparisonAlgorithm.ModifiedDateAndSize));
        //}

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

        [Fact]
        public async Task CompareAsync_FullContents_FilesDifferBeyondFirstBuffer_ReturnsModifiedStatus()
        {
            // Arrange: identical for the first 64 KB, different only in the last byte, so a
            // byte-by-byte compare that stops early (or buffers both whole files in RAM) is wrong.
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            byte[] content = new byte[100_000];
            new Random(1234).NextBytes(content);
            string leftFile = Path.Combine(leftPath, "big.bin");
            string rightFile = Path.Combine(rightPath, "big.bin");
            File.WriteAllBytes(leftFile, content);
            byte[] other = (byte[])content.Clone();
            other[^1] ^= 0xFF;
            File.WriteAllBytes(rightFile, other);

            SetupTwoFileTree(leftPath, rightPath, leftFile, rightFile);

            // Act
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.FullContents);

            // Assert
            Assert.Contains(result, i => i.Status == ComparisonStatus.Modified && i.Left?.Name == "big.bin");
            // Reads must go through IFileSystem/OpenRead in chunks, not File.ReadAllBytes.
            _fileSystemMock.Verify(fs => fs.OpenRead(leftFile), Times.Once);
            _fileSystemMock.Verify(fs => fs.OpenRead(rightFile), Times.Once);
        }

        [Fact]
        public async Task CompareAsync_FullContents_SameContentDifferentTimes_ReturnsIdenticalStatus()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            byte[] content = new byte[100_000];
            new Random(1234).NextBytes(content);
            string leftFile = Path.Combine(leftPath, "big.bin");
            string rightFile = Path.Combine(rightPath, "big.bin");
            File.WriteAllBytes(leftFile, content);
            File.WriteAllBytes(rightFile, content);

            SetupTwoFileTree(leftPath, rightPath, leftFile, rightFile);

            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.FullContents);

            Assert.Contains(result, i => i.Status == ComparisonStatus.Identical && i.Left?.Name == "big.bin");
        }

        [Fact]
        public async Task CompareAsync_CancellationRequested_ThrowsInsteadOfReportingIdentical()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _comparer.CompareAsync(leftPath, rightPath, cts.Token, null, ComparisonAlgorithm.ModifiedDateAndSize));
        }

        // FileSystemTreeBuilderFactory is static, so a mocked IFileSystemTreeBuilder is never used: the real
        // DotnetFileSystemTreeBuilder scans through these IFileSystem calls. The paths exist on disk, so the mock
        // forwards to the real Directory/File APIs (DirectoryExists must answer true for nested folders too,
        // otherwise a subfolder is built as a childless file node and its rows never appear).
        private void SetupRealScan()
        {
            _fileSystemMock.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns((string p) => Directory.Exists(p));
            _fileSystemMock.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns((string p) => File.Exists(p));
            _fileSystemMock.Setup(fs => fs.GetDirectories(It.IsAny<string>()))
                .Returns((string p) => Directory.Exists(p) ? Directory.GetDirectories(p) : Array.Empty<string>());
            _fileSystemMock.Setup(fs => fs.GetFiles(It.IsAny<string>()))
                .Returns((string p) => Directory.Exists(p) ? Directory.GetFiles(p) : Array.Empty<string>());
            _fileSystemMock.Setup(fs => fs.GetFileInfo(It.IsAny<string>())).Returns((string p) => new FileInfo(p));
            _fileSystemMock.Setup(fs => fs.GetLastWriteTime(It.IsAny<string>())).Returns((string p) => Directory.GetLastWriteTime(p));
            _fileSystemMock.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns((string p) => File.OpenRead(p));
        }

        // Wires the mocked IFileSystem up to two real files on disk, one per side.
        private void SetupTwoFileTree(string leftPath, string rightPath, string leftFile, string rightFile)
        {
            _fileSystemMock.Setup(fs => fs.DirectoryExists(leftPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetDirectories(It.IsAny<string>())).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.GetLastWriteTime(It.IsAny<string>())).Returns(DateTime.Now);
            _fileSystemMock.Setup(fs => fs.GetFiles(leftPath)).Returns(new[] { leftFile });
            _fileSystemMock.Setup(fs => fs.GetFiles(rightPath)).Returns(new[] { rightFile });
            _fileSystemMock.Setup(fs => fs.GetFileInfo(It.IsAny<string>())).Returns((string p) => new FileInfo(p));
            _fileSystemMock.Setup(fs => fs.OpenRead(It.IsAny<string>())).Returns((string p) => File.OpenRead(p));
        }

        [Fact]
        public async Task CompareAsync_ParallelProcessing_ReturnsIdenticalStatus()
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
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);

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
            var comparer = new DirectoryComparer(null, fileSystem, hashProvider);
            double progress = 0;
            var progressCallback = new Action<double>(p => progress = p);

            // Act
            var result = await comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, progressCallback, ComparisonAlgorithm.ModifiedDateAndSize);

            // Assert
            Assert.True(progress > 0, "Progress callback was not invoked.");
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.LeftOnly, result[1].Status);
        }

        // [Fact]
        // public async Task CompareAsync_NonExistentPath_ThrowsDirectoryNotFoundException()
        // {
        //     // Arrange
        //     string invalidPath = Path.Combine(_tempDir, "invalid");
        //     string rightPath = Path.Combine(_tempDir, "right");
        //     Directory.CreateDirectory(rightPath);

        //     var fileSystem = new FileSystem();
        //     var hashProvider = new HashProvider();
        //     var comparer = new DirectoryComparer(null, fileSystem, hashProvider);

        //     // Act & Assert
        //     await Assert.ThrowsAsync<DirectoryNotFoundException>(
        //         () => comparer.CompareAsync(invalidPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize));
        // }

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


            _fileSystemMock.Setup(fs => fs.DirectoryExists(leftPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(leftSubPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.DirectoryExists(rightSubPath)).Returns(true);
            _fileSystemMock.Setup(fs => fs.GetDirectories(leftPath)).Returns(new[] { leftSubPath });
            _fileSystemMock.Setup(fs => fs.GetDirectories(rightPath)).Returns(new[] { rightSubPath });
            _fileSystemMock.Setup(fs => fs.GetFiles(It.IsAny<string>())).Returns(new string[] { });
            _fileSystemMock.Setup(fs => fs.GetLastWriteTime(It.IsAny<string>())).Returns(DateTime.Now);

            // Act
            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(ComparisonStatus.Identical, result[0].Status);
            Assert.Equal(ComparisonStatus.Identical, result[1].Status);
        }

        // Regression: CompareNodes used to promote the parent through
        // result.LastOrDefault(i => i.Left == left || i.Right == right), which — with left == null — matched every
        // one-sided row and repainted the deepest descendant "Modified" while the real folder row kept its status.
        [Fact]
        public async Task CompareAsync_OneSidedFolder_KeepsFolderAndDescendantLeftOnly()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string leftSub = Path.Combine(leftPath, "a");
            Directory.CreateDirectory(rightPath);
            Directory.CreateDirectory(leftSub);
            File.WriteAllText(Path.Combine(leftSub, "b.txt"), "hello");

            SetupRealScan();

            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);

            var folder = result.Single(i => i.Left?.Name == "a");
            var file = result.Single(i => i.Left?.Name == "b.txt");
            Assert.Equal(ComparisonStatus.LeftOnly, folder.Status);
            Assert.Equal(ComparisonStatus.LeftOnly, file.Status);
            Assert.DoesNotContain(result, i => (i.Left?.Name == "a" || i.Left?.Name == "b.txt") && i.Status == ComparisonStatus.Modified);
        }

        [Fact]
        public async Task CompareAsync_MatchingNestedFolder_AllRowsIdentical()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            string leftSub = Path.Combine(leftPath, "a");
            string rightSub = Path.Combine(rightPath, "a");
            Directory.CreateDirectory(leftSub);
            Directory.CreateDirectory(rightSub);
            string leftFile = Path.Combine(leftSub, "b.txt");
            string rightFile = Path.Combine(rightSub, "b.txt");
            File.WriteAllText(leftFile, "hello");
            File.WriteAllText(rightFile, "hello");
            var stamp = new DateTime(2024, 5, 1, 12, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(leftFile, stamp);
            File.SetLastWriteTimeUtc(rightFile, stamp);

            SetupRealScan();

            var result = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);

            Assert.Equal(3, result.Count); // root, a, a/b.txt
            Assert.Equal(ComparisonStatus.Identical, result.Single(i => i.Left?.Name == "a").Status);
            Assert.Equal(ComparisonStatus.Identical, result.Single(i => i.Left?.Name == "b.txt").Status);
            Assert.All(result, i => Assert.Equal(ComparisonStatus.Identical, i.Status));
        }

        // Regression: xxHash64_Partial used to hit `default:` and compare size + modified time without hashing.
        [Fact]
        public async Task CompareAsync_XxHash64Partial_SameContentDifferentTimes_ReturnsIdentical()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            byte[] content = new byte[4096];
            new Random(7).NextBytes(content);
            string leftFile = Path.Combine(leftPath, "doc.bin");
            string rightFile = Path.Combine(rightPath, "doc.bin");
            File.WriteAllBytes(leftFile, content);
            File.WriteAllBytes(rightFile, content);
            var stamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(leftFile, stamp);
            File.SetLastWriteTimeUtc(rightFile, stamp.AddMinutes(1));

            SetupTwoFileTree(leftPath, rightPath, leftFile, rightFile);

            // Size + time alone reports Modified, so only a real content hash can report Identical here.
            var byTime = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);
            Assert.Equal(ComparisonStatus.Modified, byTime.Single(i => i.Left?.Name == "doc.bin").Status);

            var hashingComparer = new DirectoryComparer(new AppConfiguration(), _fileSystemMock.Object, new HashProvider());
            var hashed = await hashingComparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.xxHash64_Partial);
            Assert.Equal(ComparisonStatus.Identical, hashed.Single(i => i.Left?.Name == "doc.bin").Status);
        }

        [Fact]
        public async Task CompareAsync_XxHash64Partial_DifferentHeadBytesSameTimes_ReturnsModified()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            byte[] content = new byte[2048];
            new Random(11).NextBytes(content);
            byte[] other = (byte[])content.Clone();
            for (int i = 0; i < 100; i++) other[i] ^= 0xFF;

            string leftFile = Path.Combine(leftPath, "doc.bin");
            string rightFile = Path.Combine(rightPath, "doc.bin");
            File.WriteAllBytes(leftFile, content);
            File.WriteAllBytes(rightFile, other);
            var stamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(leftFile, stamp);
            File.SetLastWriteTimeUtc(rightFile, stamp);

            SetupTwoFileTree(leftPath, rightPath, leftFile, rightFile);

            // Size and modified time are equal here, so only a content hash can report Modified.
            var byTime = await _comparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize);
            Assert.Equal(ComparisonStatus.Identical, byTime.Single(i => i.Left?.Name == "doc.bin").Status);

            var hashingComparer = new DirectoryComparer(new AppConfiguration(), _fileSystemMock.Object, new HashProvider());
            var hashed = await hashingComparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.xxHash64_Partial);
            Assert.Equal(ComparisonStatus.Modified, hashed.Single(i => i.Left?.Name == "doc.bin").Status);
        }

        // Pins the wired-up Options:Compare:Algorithms[xxHash64_Partial].ComparisonPercentage (30): a difference
        // past the first 30% is deliberately not seen by Partial, but is by Full.
        [Fact]
        public async Task CompareAsync_XxHash64Partial_DifferenceBeyondConfiguredPercentage_NotSeenByPartialButByFull()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            byte[] content = new byte[4096];
            new Random(13).NextBytes(content);
            byte[] other = (byte[])content.Clone();
            other[^1] ^= 0xFF; // beyond the 30% window (first 1228 bytes)

            string leftFile = Path.Combine(leftPath, "doc.bin");
            string rightFile = Path.Combine(rightPath, "doc.bin");
            File.WriteAllBytes(leftFile, content);
            File.WriteAllBytes(rightFile, other);
            var stamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(leftFile, stamp);
            File.SetLastWriteTimeUtc(rightFile, stamp);

            SetupTwoFileTree(leftPath, rightPath, leftFile, rightFile);

            var hashingComparer = new DirectoryComparer(new AppConfiguration(), _fileSystemMock.Object, new HashProvider());
            var partial = await hashingComparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.xxHash64_Partial);
            Assert.Equal(ComparisonStatus.Identical, partial.Single(i => i.Left?.Name == "doc.bin").Status);

            var full = await hashingComparer.CompareAsync(leftPath, rightPath, CancellationToken.None, null, ComparisonAlgorithm.xxHash64_Full);
            Assert.Equal(ComparisonStatus.Modified, full.Single(i => i.Left?.Name == "doc.bin").Status);
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
            //    .Returns((FileSystemNode?)null);

            // Act
            var result = _comparer.CompareSubtree(leftPath, rightPath, subPath, CancellationToken.None, ComparisonAlgorithm.ModifiedDateAndSize);

            // Assert
            Assert.Empty(result);
        }
    }
}