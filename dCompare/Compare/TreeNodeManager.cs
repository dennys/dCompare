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
        private Predicate<TreeNode> _currentFilter;

        public TreeNodeManager(ObservableCollection<TreeNode> treeNodes)
        {
            _treeNodes = treeNodes;
        }

		// Method to set the current filter
		public void SetCurrentFilter(Predicate<TreeNode> filter)
		{
			_currentFilter = filter;
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
        /// <summary>
        /// Refreshes a subtree in the UI by updating the root node, removing its old children,
        /// and loading the new children from the updated cache.
        /// </summary>
        /// <param name="nodeToRefresh">The node representing the directory to refresh.</param>
        /// <param name="updatedCachedResults">The entire, updated list of comparison results.</param>
        /// <param name="leftRoot">The root path of the left side comparison.</param>
        /// <param name="rightRoot">The root path of the right side comparison.</param>
        public void RefreshSubtreeInView(TreeNode nodeToRefresh, List<DirectoryComparer.ComparisonItem> updatedCachedResults, string leftRoot, string rightRoot)
        {
            // Remember the node's current state
            bool wasExpanded = nodeToRefresh.IsExpanded;

            // Find the new comparison data for the node being refreshed.
            var newRootComparisonItem = updatedCachedResults.FirstOrDefault(item =>
                GetRelativePath(item, leftRoot, rightRoot).Equals(nodeToRefresh.RelativePath, StringComparison.OrdinalIgnoreCase));

            // First, remove all currently visible children from the UI.
            RemoveChildren(nodeToRefresh);

            // If the new data is null, it means the directory was deleted from both sides. Remove it from the UI.
            if (newRootComparisonItem == null)
            {
                _treeNodes.Remove(nodeToRefresh);
                return;
            }

            // Update the properties of the existing TreeNode in-place.
            // This will trigger data binding and update the single row in the ListView.
            SetNodeDetails(nodeToRefresh, newRootComparisonItem, leftRoot, rightRoot);

            // If the node was expanded before, re-load its children to show the new state.
            nodeToRefresh.IsExpanded = wasExpanded;
            if (nodeToRefresh.IsExpanded)
            {
                LoadChildren(nodeToRefresh, updatedCachedResults, leftRoot, rightRoot);
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



        public string GetRelativePath(DirectoryComparer.ComparisonItem item, string leftRoot, string rightRoot)
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