using Xunit;
using Moq;
using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.DirTreeSystem.Models;
using dCompare.FileOperation;
using System;
using System.Collections.Generic;
using System.IO;

namespace dCompare.Tests.FileOperation
{
    public class FileOperationHandlerTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _targetRoot;
        private readonly string _sourceSide = Path.Combine("C:", "dCompare-tests", "source");
        private readonly FileOperationHandler _handler;

        public FileOperationHandlerTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "FileOperationHandlerTests", Path.GetRandomFileName());
            _targetRoot = Path.Combine(_testRoot, "target");
            Directory.CreateDirectory(_targetRoot);

            _handler = new FileOperationHandler(new Mock<IFileManager>().Object);
        }

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

        // An empty or relative root used to flow into Path.Combine, so the user's files landed under the
        // process working directory instead of the compared folder.
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("relative\\dir")]
        public void GetItemsForTransfer_RelativeOrEmptyTargetRoot_ThrowsArgumentException(string targetRoot)
        {
            var items = new List<TreeNode> { BuildNode(toLeft: false) };

            var exception = Assert.Throws<ArgumentException>(() => _handler.GetItemsForTransfer(items, toLeft: false, targetRoot));

            Assert.Equal("targetRoot", exception.ParamName);
        }

        [Fact]
        public void GetItemsForTransfer_RootedTargetRoot_TargetPathUnderThatRoot()
        {
            var items = new List<TreeNode> { BuildNode(toLeft: false) };

            var result = _handler.GetItemsForTransfer(items, toLeft: false, _targetRoot);

            var item = Assert.Single(result);
            Assert.Equal(SourceType.Left, item.Type);
            Assert.Equal(Path.Combine(_sourceSide, "a", "b.txt"), item.Source);
            Assert.Equal(Path.Combine(_targetRoot, "a", "b.txt"), item.TargetPath);
            Assert.StartsWith(_targetRoot, item.TargetPath, StringComparison.Ordinal);
            Assert.True(Path.IsPathRooted(item.TargetPath));
        }

        [Fact]
        public void GetItemsForTransfer_ToLeft_TakesRightNodeAndRightSourceType()
        {
            var items = new List<TreeNode> { BuildNode(toLeft: true) };

            var result = _handler.GetItemsForTransfer(items, toLeft: true, _targetRoot);

            var item = Assert.Single(result);
            Assert.Equal(SourceType.Right, item.Type);
            Assert.Equal(Path.Combine(_sourceSide, "a", "r.txt"), item.Source);
            Assert.Equal(Path.Combine(_targetRoot, "a", "r.txt"), item.TargetPath);
        }

        // GetItemsForTransfer only reads RelativePath plus the FullPath of the side being copied from,
        // so nothing touches the disk and the source paths can stay synthetic.
        private TreeNode BuildNode(bool toLeft)
        {
            string fileName = toLeft ? "r.txt" : "b.txt";
            var node = new FileSystemNode
            {
                Name = fileName,
                FullPath = Path.Combine(_sourceSide, "a", fileName),
                IsDirectory = false
            };

            return new TreeNode
            {
                Name = fileName,
                RelativePath = Path.Combine("a", fileName),
                IsDirectory = false,
                LeftNode = toLeft ? null : node,
                RightNode = toLeft ? node : null
            };
        }
    }
}
