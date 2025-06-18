using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace dCompare.Compare
{
    public class TreeNodeManager
    {
        private readonly ObservableCollection<TreeNode> _treeNodes;

        public TreeNodeManager(ObservableCollection<TreeNode> treeNodes)
        {
            _treeNodes = treeNodes;
        }

        public void BuildTree(List<DirectoryComparer.ComparisonItem> items, string leftRoot, string rightRoot, int maxDepth = 0)
        {
            _treeNodes.Clear();
            var flatNodes = CreateFlatTreeStructure(items, leftRoot, rightRoot, maxDepth);
            foreach (var node in flatNodes)
            {
                node.IsExpanded = false;
                _treeNodes.Add(node);
            }
        }

        public List<TreeNode> CreateFlatTreeStructure(List<DirectoryComparer.ComparisonItem> items, string leftRoot, string rightRoot, int maxDepth = int.MaxValue)
        {
            List<TreeNode> flatNodes = new();
            foreach (var item in items)
            {
                string relativePath = GetRelativePath(item, leftRoot, rightRoot);
                if (string.IsNullOrEmpty(relativePath)) continue;

                int depth = relativePath.Count(c => c == Path.DirectorySeparatorChar);
                if (depth > maxDepth) continue;

                TreeNode node = new()
                {
                    Name = item.Left?.Name ?? item.Right?.Name ?? Path.GetFileName(relativePath),
                    RelativePath = relativePath,
                    IsDirectory = item.Left?.IsDirectory ?? item.Right?.IsDirectory ?? false,
                    Depth = depth
                };

                SetNodeDetails(node, item, leftRoot, rightRoot);
                flatNodes.Add(node);
            }
            return flatNodes;
        }

        public void LoadChildren(TreeNode parentNode, List<DirectoryComparer.ComparisonItem> cachedResults, string leftRoot, string rightRoot)
        {
            if (cachedResults == null) return;

            var childItems = cachedResults
                .Where(item =>
                {
                    string relativePath = GetRelativePath(item, leftRoot, rightRoot);
                    return !string.IsNullOrEmpty(relativePath) &&
                           relativePath.StartsWith(parentNode.RelativePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                           relativePath.Count(c => c == Path.DirectorySeparatorChar) == parentNode.Depth + 1;
                })
                .ToList();

            var childNodes = CreateFlatTreeStructure(childItems, leftRoot, rightRoot, parentNode.Depth + 1);
            int insertIndex = _treeNodes.IndexOf(parentNode) + 1;

            foreach (var child in childNodes)
            {
                child.IsExpanded = false;
                _treeNodes.Insert(insertIndex++, child);
            }
        }

        public void RemoveChildren(TreeNode parentNode)
        {
            var children = _treeNodes
                .Where(n => n.RelativePath.StartsWith(parentNode.RelativePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var child in children)
            {
                _treeNodes.Remove(child);
            }
        }

        public TreeNode FindOrCreateNode(string relativePath, bool isDirectory)
        {
            var node = _treeNodes.FirstOrDefault(n => n.RelativePath == relativePath);
            if (node == null)
            {
                node = new TreeNode
                {
                    RelativePath = relativePath,
                    Name = Path.GetFileName(relativePath),
                    IsDirectory = isDirectory,
                    Depth = relativePath.Count(c => c == Path.DirectorySeparatorChar)
                };
                int insertIndex = _treeNodes.TakeWhile(n => string.Compare(n.LeftDisplayName, node.LeftDisplayName, StringComparison.OrdinalIgnoreCase) < 0).Count();
                _treeNodes.Insert(insertIndex, node);
            }
            return node;
        }

        public void UpdateNodeAfterOperation(TreeNode node, string leftPath, string rightPath, bool isDirectory)
        {
            if (!string.IsNullOrEmpty(leftPath) && (File.Exists(leftPath) || Directory.Exists(leftPath)))
            {
                node.LeftNode = new FileSystemNode
                {
                    FullPath = leftPath,
                    Name = Path.GetFileName(leftPath),
                    IsDirectory = isDirectory,
                    Size = isDirectory ? 0 : new FileInfo(leftPath).Length,
                    ModifiedTime = isDirectory ? Directory.GetLastWriteTime(leftPath) : File.GetLastWriteTime(leftPath)
                };
                node.LeftName = node.LeftNode.Name;
                node.LeftSize = FileSystemNode.FormatSize(node.LeftNode.Size);
                node.LeftDate = node.LeftNode.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }
            else
            {
                node.LeftNode = null;
                node.LeftName = null;
                node.LeftSize = null;
                node.LeftDate = null;
            }

            if (!string.IsNullOrEmpty(rightPath) && (File.Exists(rightPath) || Directory.Exists(rightPath)))
            {
                node.RightNode = new FileSystemNode
                {
                    FullPath = rightPath,
                    Name = Path.GetFileName(rightPath),
                    IsDirectory = isDirectory,
                    Size = isDirectory ? 0 : new FileInfo(rightPath).Length,
                    ModifiedTime = isDirectory ? Directory.GetLastWriteTime(rightPath) : File.GetLastWriteTime(rightPath)
                };
                node.RightName = node.RightNode.Name;
                node.RightSize = FileSystemNode.FormatSize(node.RightNode.Size);
                node.RightDate = node.RightNode.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }
            else
            {
                node.RightNode = null;
                node.RightName = null;
                node.RightSize = null;
                node.RightDate = null;
            }

            if (node.LeftNode != null && node.RightNode != null)
            {
                bool areIdentical = node.LeftNode.Size == node.RightNode.Size &&
                                    node.LeftNode.ModifiedTime == node.RightNode.ModifiedTime;
                node.Status = areIdentical ? ComparisonStatus.Identical : ComparisonStatus.Modified;
                node.StatusSymbol = areIdentical ? "=" : "≠";
            }
            else if (node.LeftNode != null)
            {
                node.Status = ComparisonStatus.LeftOnly;
                node.StatusSymbol = "<";
            }
            else if (node.RightNode != null)
            {
                node.Status = ComparisonStatus.RightOnly;
                node.StatusSymbol = ">";
            }
            else
            {
                node.Status = ComparisonStatus.Conflict;
                node.StatusSymbol = "X";
            }

            node.Name = node.LeftNode?.Name ?? node.RightNode?.Name ?? node.Name;
            node.IsDirectory = isDirectory;
        }

        private string GetRelativePath(DirectoryComparer.ComparisonItem item, string leftRoot, string rightRoot)
        {
            string path = "";
            if (item.Left != null && item.Left.FullPath.StartsWith(leftRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = item.Left.FullPath.Substring(leftRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            else if (item.Right != null && item.Right.FullPath.StartsWith(rightRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = item.Right.FullPath.Substring(rightRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            return path;
        }

        private void SetNodeDetails(TreeNode node, DirectoryComparer.ComparisonItem item, string leftRoot, string rightRoot)
        {
            node.LeftNode = item.Left;
            node.RightNode = item.Right;
            node.Status = item.Status;

            if (item.Left != null)
            {
                node.LeftName = item.Left.Name;
                node.LeftSize = FileSystemNode.FormatSize(item.Left.Size);
                node.LeftDate = item.Left.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }

            if (item.Right != null)
            {
                node.RightName = item.Right.Name;
                node.RightSize = FileSystemNode.FormatSize(item.Right.Size);
                node.RightDate = item.Right.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }

            node.StatusSymbol = item.Status switch
            {
                ComparisonStatus.Identical => "=",
                ComparisonStatus.Modified => "≠",
                ComparisonStatus.LeftOnly => "<",
                ComparisonStatus.RightOnly => ">",
                ComparisonStatus.Conflict => "X",
                _ => "?"
            };
        }
    }
}