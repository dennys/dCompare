using dCompare.Compare;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.DirTreeSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace dCompare.Tests.DirTreeSystem
{
    /// <summary>
    /// A folder that cannot be listed must not compare as "identical" or as "missing on the other side": the
    /// user is shown the difference they cannot see and then offered the chance to delete the good files.
    /// </summary>
    public class DotnetFileSystemTreeBuilderTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _left;
        private readonly string _right;

        public DotnetFileSystemTreeBuilderTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "DotnetTreeTests", Path.GetRandomFileName());
            _left = Path.Combine(_testRoot, "left");
            _right = Path.Combine(_testRoot, "right");
            foreach (string side in new[] { _left, _right })
            {
                string locked = Path.Combine(side, "locked");
                Directory.CreateDirectory(locked);
                File.WriteAllText(Path.Combine(side, "ok.txt"), "same");
                File.WriteAllText(Path.Combine(locked, "inner.txt"), "same");
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
            }
            catch (IOException)
            {
            }
        }

        private sealed class UnreadableFolderFileSystem : IFileSystem
        {
            private readonly FileSystem _inner = new();
            public string? FolderWithDeniedFileListing = null;

            public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
            public bool FileExists(string path) => _inner.FileExists(path);
            public IEnumerable<string> GetDirectories(string path) => _inner.GetDirectories(path);
            public DateTime GetLastWriteTime(string path) => _inner.GetLastWriteTime(path);
            public FileInfo GetFileInfo(string path) => _inner.GetFileInfo(path);
            public Stream OpenRead(string path) => _inner.OpenRead(path);

            public IEnumerable<string> GetFiles(string path)
            {
                if (string.Equals(path, FolderWithDeniedFileListing, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Simulated access denied.");
                }
                return _inner.GetFiles(path);
            }
        }

        [Fact]
        public void FolderWhoseFileListingThrows_IsMarkedScanIncomplete()
        {
            var fileSystem = new UnreadableFolderFileSystem
            {
                FolderWithDeniedFileListing = Path.Combine(_left, "locked")
            };

            var root = new DotnetFileSystemTreeBuilder(fileSystem).BuildTreeRecursive(_left, 0, CancellationToken.None, null);

            // The unreadable subtree flags itself, and the flag travels up so the ancestor row cannot read clean.
            var locked = root.Children.Single(c => c.IsDirectory);
            Assert.Equal("locked", locked.Name);
            Assert.True(locked.ScanIncomplete);
            Assert.True(root.ScanIncomplete);
            Assert.All(root.Children.Where(c => !c.IsDirectory), file => Assert.False(file.ScanIncomplete));
        }

        [Fact]
        public void FolderWhoseFileListingThrows_ComparesAsConflictNotIdentical()
        {
            var fileSystem = new UnreadableFolderFileSystem
            {
                FolderWithDeniedFileListing = Path.Combine(_left, "locked")
            };
            var comparer = new DirectoryComparer(fileSystem: fileSystem);

            List<DirectoryComparer.ComparisonItem> items = comparer
                .CompareAsync(_left, _right, CancellationToken.None, null, ComparisonAlgorithm.ModifiedDateAndSize)
                .GetAwaiter().GetResult();

            DirectoryComparer.ComparisonItem lockedRow = items.Single(i =>
                i.Left?.IsDirectory == true && string.Equals(i.Left.Name, "locked", StringComparison.OrdinalIgnoreCase));

            // Both sides really hold the same file, so the only honest answer is "this side could not be read".
            Assert.Equal(ComparisonStatus.Conflict, lockedRow.Status);
        }
    }
}
