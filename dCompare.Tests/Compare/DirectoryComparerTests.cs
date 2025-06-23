using dCompare.Compare;
using dCompare.Configuration;
using System.IO;
using Xunit;

namespace dCompare.Tests
{
    /// <summary>
    /// Test fixture for DirectoryComparer. Creates temporary directories and files for tests.
    /// </summary>
    public class DirectoryComparerTestFixture : IDisposable
    {
        public string LeftPath { get; }
        public string RightPath { get; }

        public DirectoryComparerTestFixture()
        {
            // Create unique root directories for test run
            string baseTestDir = Path.Combine(Path.GetTempPath(), "DirectoryComparerTests", Guid.NewGuid().ToString());
            LeftPath = Path.Combine(baseTestDir, "left");
            RightPath = Path.Combine(baseTestDir, "right");

            // Create directories
            Directory.CreateDirectory(LeftPath);
            Directory.CreateDirectory(RightPath);
            Directory.CreateDirectory(Path.Combine(LeftPath, "subdir"));
            Directory.CreateDirectory(Path.Combine(RightPath, "subdir"));

            // Create test files
            // 1. Identical file
            File.WriteAllText(Path.Combine(LeftPath, "identical.txt"), "This is an identical file.");
            File.WriteAllText(Path.Combine(RightPath, "identical.txt"), "This is an identical file.");

            // 2. Modified file
            File.WriteAllText(Path.Combine(LeftPath, "modified.txt"), "This is the original file.");
            File.WriteAllText(Path.Combine(RightPath, "modified.txt"), "This is the modified file.");

            // 3. File only in left
            File.WriteAllText(Path.Combine(LeftPath, "left-only.txt"), "This file is only in the left directory.");

            // 4. File only in right
            File.WriteAllText(Path.Combine(RightPath, "right-only.txt"), "This file is only in the right directory.");

            // 5. Files in subdirectory
            File.WriteAllText(Path.Combine(LeftPath, "subdir", "subfile.txt"), "Subdirectory file.");
            File.WriteAllText(Path.Combine(RightPath, "subdir", "subfile.txt"), "Subdirectory file.");
        }

        public void Dispose()
        {
            // Cleanup: delete the temporary directories and files
            try
            {
                if (Directory.Exists(LeftPath))
                {
                    Directory.Delete(Path.GetDirectoryName(LeftPath)!, recursive: true);
                }
                if (Directory.Exists(RightPath))
                {
                    Directory.Delete(Path.GetDirectoryName(RightPath)!, recursive: true);
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// xUnit tests for the DirectoryComparer class.
    /// This uses a test fixture to share setup and cleanup logic.
    /// </summary>
    public class DirectoryComparerTests : IClassFixture<DirectoryComparerTestFixture>
    {
        private readonly DirectoryComparerTestFixture _fixture;
        private readonly DirectoryComparer _comparer;

        public DirectoryComparerTests(DirectoryComparerTestFixture fixture)
        {
            _fixture = fixture;
            _comparer = new DirectoryComparer();
        }

        [Fact]
        public void Compare_Should_Identify_Identical_File()
        {
            // Act
            var result = _comparer.Compare(_fixture.LeftPath, _fixture.RightPath);

            // Assert
            var identicalFile = result.FirstOrDefault(item => item.Left?.Name == "identical.txt");
            Assert.NotNull(identicalFile);
            Assert.Equal(ComparisonStatus.Identical, identicalFile.Status);
        }

        [Fact]
        public void Compare_Should_Identify_Modified_File()
        {
            // Act
            var result = _comparer.Compare(_fixture.LeftPath, _fixture.RightPath, algorithm: ComparisonAlgorithm.FullContents);

            // Assert
            var modifiedFile = result.FirstOrDefault(item => item.Left?.Name == "modified.txt");
            Assert.NotNull(modifiedFile);
            Assert.Equal(ComparisonStatus.Modified, modifiedFile.Status);
        }

        [Fact]
        public void Compare_Should_Identify_LeftOnly_File()
        {
            // Act
            var result = _comparer.Compare(_fixture.LeftPath, _fixture.RightPath);

            // Assert
            var leftOnlyFile = result.FirstOrDefault(item => item.Left?.Name == "left-only.txt");
            Assert.NotNull(leftOnlyFile);
            Assert.Equal(ComparisonStatus.LeftOnly, leftOnlyFile.Status);
        }

        [Fact]
        public void Compare_Should_Identify_RightOnly_File()
        {
            // Act
            var result = _comparer.Compare(_fixture.LeftPath, _fixture.RightPath);

            // Assert
            var rightOnlyFile = result.FirstOrDefault(item => item.Right?.Name == "right-only.txt");
            Assert.NotNull(rightOnlyFile);
            Assert.Equal(ComparisonStatus.RightOnly, rightOnlyFile.Status);
        }

        [Fact]
        public void Compare_Should_Handle_Subdirectories_Correctly()
        {
            // Act
            var result = _comparer.Compare(_fixture.LeftPath, _fixture.RightPath);

            // Assert
            var subDir = result.FirstOrDefault(item => (item.Left?.Name ?? item.Right?.Name) == "subdir");
            Assert.NotNull(subDir);
            Assert.True(subDir.Left.IsDirectory, "Subdirectory on the left should be identified as a directory.");
            Assert.True(subDir.Right.IsDirectory, "Subdirectory on the right should be identified as a directory.");

            var subFile = result.FirstOrDefault(item => (item.Left?.Name ?? item.Right?.Name) == "subfile.txt" && (item.Left?.FullPath.Contains("subdir") ?? item.Right.FullPath.Contains("subdir")));
            Assert.NotNull(subFile);
            Assert.Equal(ComparisonStatus.Identical, subFile.Status);
        }

        [Fact]
        //public void CompareSubtree_Should_Compare_Only_Specified_Subdirectory()
        //{
        //    // Act
        //    var result = _comparer.CompareSubtree(_fixture.LeftPath, _fixture.RightPath, "subdir", CancellationToken.None, ComparisonAlgorithm.ModifiedDateAndSize);

        //    // Assert
        //    Assert.Single(result.Where(r => r.Left != null || r.Right != null)); // Root of subtree
        //    var subFile = result.FirstOrDefault(item => (item.Left?.Name ?? item.Right?.Name) == "subfile.txt");
        //    Assert.NotNull(subFile);
        //    Assert.Equal(ComparisonStatus.Identical, subFile.Status);
        //}

        [Theory]
        [InlineData(ComparisonAlgorithm.ModifiedDateAndSize)]
        [InlineData(ComparisonAlgorithm.FullContents)]
        [InlineData(ComparisonAlgorithm.MD5)]
        [InlineData(ComparisonAlgorithm.xxHash64)]
        public void Compare_With_Different_Algorithms_Should_Work(ComparisonAlgorithm algorithm)
        {
            // Act
            var result = _comparer.Compare(_fixture.LeftPath, _fixture.RightPath, algorithm: algorithm);

            // Assert
            var identicalFile = result.FirstOrDefault(item => item.Left?.Name == "identical.txt");
            var modifiedFile = result.FirstOrDefault(item => item.Left?.Name == "modified.txt");

            Assert.NotNull(identicalFile);
            Assert.Equal(ComparisonStatus.Identical, identicalFile.Status);

            Assert.NotNull(modifiedFile);
            Assert.Equal(ComparisonStatus.Modified, modifiedFile.Status);
        }

        [Fact]
        public async Task Compare_Should_Be_Cancellable()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var token = cts.Token;

            // Act & Assert
            var comparisonTask = Task.Run(() => _comparer.Compare(_fixture.LeftPath, _fixture.RightPath, token));
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await comparisonTask);
        }
    }
}
