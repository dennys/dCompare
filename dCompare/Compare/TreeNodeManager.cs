using dCompare.Compare.Models;
using dCompare.DirTreeSystem.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace dCompare.Compare
{
    public class TreeNodeManager(ObservableCollection<TreeNode> treeNodes)
    {
        private readonly ObservableCollection<TreeNode> _treeNodes = treeNodes;
        private Predicate<TreeNode>? _currentFilter;
        private string _searchFilterText = string.Empty;

        // Method to set the current filter
        public void SetCurrentFilter(Predicate<TreeNode>? filter, string searchFilterText)
		{
			_currentFilter = filter;
            _searchFilterText = searchFilterText;
		}

        public void BuildTree(List<DirectoryComparer.ComparisonItem> items, string leftRoot, string rightRoot, int maxDepth = 0)
        {
            _treeNodes.Clear();
            List<TreeNode> flatNodes = CreateFlatTreeStructure(items, leftRoot, rightRoot, maxDepth);
            foreach (TreeNode node in flatNodes)
            {
                if (MatchesSearch(node))
                {
                    node.IsExpanded = false;
                    _treeNodes.Add(node);
                }
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

                SetNodeDetails(node, item);
                flatNodes.Add(node);
            }
            return flatNodes;
        }
        public void LoadChildren(TreeNode parentNode, List<DirectoryComparer.ComparisonItem> cachedResults, string leftRoot, string rightRoot, HashSet<string> previouslyExpandedPaths = null)
        {
            if (cachedResults == null) return;

            var childItems = cachedResults
                .Where(item =>
                {
                    string relativePath = GetRelativePath(item, leftRoot, rightRoot);
                    if (string.IsNullOrEmpty(relativePath)) return false;

                    string parentPath = Path.GetDirectoryName(relativePath) ?? "";  // Handle root-level items, whose parent path is empty
                    return parentPath.Equals(parentNode.RelativePath, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (childItems.Count == 0) return;

            List<TreeNode> childNodes = CreateFlatTreeStructure(childItems, leftRoot, rightRoot, parentNode.Depth + 1);
            int insertIndex = _treeNodes.IndexOf(parentNode) + 1;
            if (insertIndex == 0) return; // Parent not found in collection

            foreach (TreeNode child in childNodes.OrderBy(c => c.Name))
            {
                if (MatchesSearch(child))
                {
                    _treeNodes.Insert(insertIndex++, child);

                    // *** If the child path was previously expanded, expand it and load its children recursively. ***
                    if (child.IsDirectory && previouslyExpandedPaths != null && previouslyExpandedPaths.Contains(child.RelativePath))
                    {
                        child.IsExpanded = true;
                        // The recursive call will now insert this child's children at the correct, updated index.
                        LoadChildren(child, cachedResults, leftRoot, rightRoot, previouslyExpandedPaths);
                        // We need to update the insertIndex to account for the grandchildren that were just added.
                        insertIndex = _treeNodes.IndexOf(child) + 1 + childNodes.Count(n => n.RelativePath.StartsWith(child.RelativePath + Path.DirectorySeparatorChar));
                    }
                    else
                    {
                        child.IsExpanded = false;
                    }
                }
            }
        }

        private bool MatchesSearch(TreeNode node)
        {
            if (string.IsNullOrEmpty(_searchFilterText) || _searchFilterText == "Search...")
            {
                return true;
            }

            return (node.LeftName?.Contains(_searchFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (node.RightName?.Contains(_searchFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (node.LeftSize?.Contains(_searchFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (node.RightSize?.Contains(_searchFilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (node.Status.ToString().Contains(_searchFilterText, StringComparison.OrdinalIgnoreCase));
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
        public void RefreshSubtreeInView(TreeNode nodeToRefresh, List<DirectoryComparer.ComparisonItem> updatedCachedResults, string leftRoot, string rightRoot, HashSet<string> previouslyExpandedPaths)
        {
            // Remember the node's own current state, as it might have just been changed by the user.
            bool wasExpanded = nodeToRefresh.IsExpanded;

            var newRootComparisonItem = updatedCachedResults.FirstOrDefault(item =>
                GetRelativePath(item, leftRoot, rightRoot).Equals(nodeToRefresh.RelativePath, StringComparison.OrdinalIgnoreCase));

            // First, remove all currently visible children from the UI.
            RemoveChildren(nodeToRefresh);

            if (newRootComparisonItem == null)
            {
                _treeNodes.Remove(nodeToRefresh);
                return;
            }

            SetNodeDetails(nodeToRefresh, newRootComparisonItem);

            // Restore the node's expanded state.
            nodeToRefresh.IsExpanded = wasExpanded;

            // If the node should be expanded, reload its children, restoring their states as well.
            if (nodeToRefresh.IsExpanded)
            {
                LoadChildren(nodeToRefresh, updatedCachedResults, leftRoot, rightRoot, previouslyExpandedPaths);
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

        private void SetNodeDetails(TreeNode node, DirectoryComparer.ComparisonItem item)
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
            else
            {
                node.LeftName = null;
                node.LeftSize = null;
                node.LeftDate = null;
            }

            if (item.Right != null)
            {
                node.RightName = item.Right.Name;
                node.RightSize = FileSystemNode.FormatSize(item.Right.Size);
                node.RightDate = item.Right.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }
            else
            {
                node.RightName = null;
                node.RightSize = null;
                node.RightDate = null;
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