using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using dCompare.Compare;
using dCompare.Compare.Models;
using dCompare.DirTreeSystem;
using Xunit;

namespace dCompare.Tests.Compare
{
    public class TreeNodeManagerTests
    {
        private readonly ObservableCollection<TreeNode> _treeNodes;
        private readonly TreeNodeManager _manager;

        public TreeNodeManagerTests()
        {
            _treeNodes = new ObservableCollection<TreeNode>();
            _manager = new TreeNodeManager(_treeNodes);
        }

        [Fact]
        public void BuildTree_ClearsExistingNodesAndAddsNewNodes()
        {
            // Arrange
            var items = new List<DirectoryComparer.ComparisonItem>
            {
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\file1.txt", Name = "file1.txt", IsDirectory = false, Size = 100, ModifiedTime = DateTime.Now },
                    Status = ComparisonStatus.LeftOnly
                }
            };
            string leftRoot = @"C:\Root\", rightRoot = @"D:\Root\";
            _treeNodes.Add(new TreeNode { Name = "existing", RelativePath = "existing" });

            // Act
            _manager.BuildTree(items, leftRoot, rightRoot);

            // Assert
            Assert.Single(_treeNodes);
            Assert.Equal("file1.txt", _treeNodes[0].Name);
            Assert.Equal("file1.txt", _treeNodes[0].RelativePath);
            Assert.False(_treeNodes[0].IsDirectory);
            Assert.Equal(0, _treeNodes[0].Depth);
            Assert.Equal(ComparisonStatus.LeftOnly, _treeNodes[0].Status);
        }

        [Fact]
        public void BuildTree_WithMaxDepth_IgnoresNodesBeyondDepth()
        {
            // Arrange
            var items = new List<DirectoryComparer.ComparisonItem>
            {
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\folder1\file1.txt", Name = "file1.txt", IsDirectory = false },
                    Status = ComparisonStatus.LeftOnly
                },
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\file2.txt", Name = "file2.txt", IsDirectory = false },
                    Status = ComparisonStatus.LeftOnly
                }
            };
            string leftRoot = @"C:\Root\", rightRoot = @"D:\Root\";

            // Act
            _manager.BuildTree(items, leftRoot, rightRoot, maxDepth: 0);

            // Assert
            Assert.Single(_treeNodes);
            Assert.Equal("file2.txt", _treeNodes[0].Name);
        }

        [Fact]
        public void LoadChildren_AddsChildNodesAtCorrectIndex()
        {
            // Arrange
            var parentNode = new TreeNode
            {
                Name = "parent",
                RelativePath = "parent",
                IsDirectory = true,
                Depth = 0,
                IsExpanded = true
            };
            _treeNodes.Add(parentNode);
            var items = new List<DirectoryComparer.ComparisonItem>
            {
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\parent\child.txt", Name = "child.txt", IsDirectory = false },
                    Status = ComparisonStatus.LeftOnly
                }
            };
            string leftRoot = @"C:\Root\", rightRoot = @"D:\Root\";

            // Act
            _manager.LoadChildren(parentNode, items, leftRoot, rightRoot);

            // Assert
            Assert.Equal(2, _treeNodes.Count);
            Assert.Equal("parent", _treeNodes[0].Name);
            Assert.Equal("child.txt", _treeNodes[1].Name);
            Assert.Equal("parent" + Path.DirectorySeparatorChar + "child.txt", _treeNodes[1].RelativePath);
            Assert.Equal(1, _treeNodes[1].Depth);
        }

        [Fact]
        public void LoadChildren_WithPreviouslyExpandedPaths_ExpandsMatchingNodes()
        {
            // Arrange
            var parentNode = new TreeNode
            {
                Name = "parent",
                RelativePath = "parent",
                IsDirectory = true,
                Depth = 0,
                IsExpanded = true
            };
            _treeNodes.Add(parentNode);
            var items = new List<DirectoryComparer.ComparisonItem>
            {
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\parent\child", Name = "child", IsDirectory = true },
                    Status = ComparisonStatus.LeftOnly
                },
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\parent\child\grandchild.txt", Name = "grandchild.txt", IsDirectory = false },
                    Status = ComparisonStatus.LeftOnly
                }
            };
            string leftRoot = @"C:\Root\", rightRoot = @"D:\Root\";
            var expandedPaths = new HashSet<string> { "parent" + Path.DirectorySeparatorChar + "child" };

            // Act
            _manager.LoadChildren(parentNode, items, leftRoot, rightRoot, expandedPaths);

            // Assert
            Assert.Equal(3, _treeNodes.Count);
            Assert.Equal("child", _treeNodes[1].Name);
            Assert.True(_treeNodes[1].IsExpanded);
            Assert.Equal("grandchild.txt", _treeNodes[2].Name);
        }

        [Fact]
        public void RemoveChildren_RemovesAllChildNodes()
        {
            // Arrange
            var parentNode = new TreeNode
            {
                Name = "parent",
                RelativePath = "parent",
                IsDirectory = true,
                Depth = 0
            };
            var childNode = new TreeNode
            {
                Name = "child",
                RelativePath = "parent" + Path.DirectorySeparatorChar + "child",
                Depth = 1
            };
            _treeNodes.Add(parentNode);
            _treeNodes.Add(childNode);

            // Act
            _manager.RemoveChildren(parentNode);

            // Assert
            Assert.Single(_treeNodes);
            Assert.Equal("parent", _treeNodes[0].Name);
        }

        [Fact]
        public void RefreshSubtreeInView_UpdatesNodeAndReloadsChildren()
        {
            // Arrange
            var nodeToRefresh = new TreeNode
            {
                Name = "folder",
                RelativePath = "folder",
                IsDirectory = true,
                Depth = 0,
                IsExpanded = true
            };
            _treeNodes.Add(nodeToRefresh);
            var updatedItems = new List<DirectoryComparer.ComparisonItem>
            {
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\folder", Name = "folder", IsDirectory = true },
                    Right = new FileSystemNode { FullPath = @"D:\Root\folder", Name = "folder", IsDirectory = true },
                    Status = ComparisonStatus.Identical
                },
                new DirectoryComparer.ComparisonItem
                {
                    Left = new FileSystemNode { FullPath = @"C:\Root\folder\newchild.txt", Name = "newchild.txt", IsDirectory = false },
                    Status = ComparisonStatus.LeftOnly
                }
            };
            string leftRoot = @"C:\Root\", rightRoot = @"D:\
oot\";

            // Act
            _manager.RefreshSubtreeInView(nodeToRefresh, updatedItems, leftRoot, rightRoot, new HashSet<string>());

            // Assert
            Assert.Equal(2, _treeNodes.Count);
            Assert.Equal(ComparisonStatus.Identical, _treeNodes[0].Status);
            Assert.Equal("newchild.txt", _treeNodes[1].Name);
            Assert.Equal("folder" + Path.DirectorySeparatorChar + "newchild.txt", _treeNodes[1].RelativePath);
        }

        [Fact]
        public void FindOrCreateNode_ReturnsExistingNodeIfFound()
        {
            // Arrange
            var existingNode = new TreeNode
            {
                Name = "file.txt",
                RelativePath = "file.txt",
                IsDirectory = false,
                Depth = 0
            };
            _treeNodes.Add(existingNode);

            // Act
            var result = _manager.FindOrCreateNode("file.txt", false);

            // Assert
            Assert.Same(existingNode, result);
            Assert.Single(_treeNodes);
        }

        [Fact]
        public void FindOrCreateNode_CreatesAndInsertsNewNodeIfNotFound()
        {
            // Arrange
            var existingNode = new TreeNode
            {
                Name = "b.txt",
                RelativePath = "b.txt",
                //LeftDisplayName = "📄 b.txt",
                IsDirectory = false,
                Depth = 0
            };
            _treeNodes.Add(existingNode);

            // Act
            var result = _manager.FindOrCreateNode("a.txt", false);

            // Assert
            Assert.Equal(2, _treeNodes.Count);
            Assert.Equal("a.txt", _treeNodes[0].Name);
            Assert.Equal("a.txt", _treeNodes[0].RelativePath);
            Assert.False(_treeNodes[0].IsDirectory);
            Assert.Equal(0, _treeNodes[0].Depth);
            Assert.Equal("b.txt", _treeNodes[1].Name);
        }

        [Fact]
        public void GetRelativePath_ReturnsCorrectPathForLeftNode()
        {
            // Arrange
            var item = new DirectoryComparer.ComparisonItem
            {
                Left = new FileSystemNode { FullPath = @"C:\Root\folder\file.txt", Name = "file.txt" }
            };
            string leftRoot = @"C:\Root\", rightRoot = @"D:\Root\";

            // Act
            var result = _manager.GetRelativePath(item, leftRoot, rightRoot);

            // Assert
            Assert.Equal("folder" + Path.DirectorySeparatorChar + "file.txt", result);
        }

        [Fact]
        public void GetRelativePath_ReturnsCorrectPathForRightNode()
        {
            // Arrange
            var item = new DirectoryComparer.ComparisonItem
            {
                Right = new FileSystemNode { FullPath = @"D:\Root\folder\file.txt", Name = "file.txt" }
            };
            string leftRoot = @"C:\Root\", rightRoot = @"D:\Root\";

            // Act
            var result = _manager.GetRelativePath(item, leftRoot, rightRoot);

            // Assert
            Assert.Equal("folder" + Path.DirectorySeparatorChar + "file.txt", result);
        }
    }
}