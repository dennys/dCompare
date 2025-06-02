using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace dCompare
{
    public class DirectoryComparer
    {
        public class ComparisonItem
        {
            public ComparisonStatus Status { get; set; }
            public string TreeIndent { get; set; } = "";
            public FileSystemNode? Left { get; set; }
            public FileSystemNode? Right { get; set; }
        }

        public List<ComparisonItem> Compare(string leftPath, string rightPath)
        {
            FileSystemNode? leftRoot = BuildTree(leftPath, 0);
            FileSystemNode? rightRoot = BuildTree(rightPath, 0);
            List<ComparisonItem> result = new();

            // Add root directory comparison with a placeholder status
            ComparisonItem? rootItem = new ComparisonItem
            {
                Status = ComparisonStatus.Identical, // Initial status, will be updated
                Left = leftRoot,
                Right = rightRoot,
                TreeIndent = ""
            };
            result.Add(rootItem);

            // Compare nodes and update directory statuses
            bool hasDifferences = CompareNodes(leftRoot, rightRoot, result, "");

            // Update root status based on children
            if (hasDifferences)
            {
                rootItem.Status = ComparisonStatus.Modified;
            }

            return result;
        }

        private FileSystemNode BuildTree(string path, int depth)
        {
            FileSystemNode? root = new FileSystemNode
            {
                Name = depth == 0 ? Path.GetFileName(path) : Path.GetFileName(path),
                FullPath = path,
                IsDirectory = Directory.Exists(path),
                Depth = depth
            };

            if (root.IsDirectory && Directory.Exists(path))
            {
                root.ModifiedTime = Directory.GetLastWriteTime(path);

                try
                {
                    // Process subdirectories
                    foreach (string? dir in Directory.GetDirectories(path))
                    {
                        root.Children.Add(BuildTree(dir, depth + 1));
                    }

                    // Process files
                    foreach (string? file in Directory.GetFiles(path))
                    {
                        root.Children.Add(BuildTree(file, depth + 1));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore directories with no access
                }
                catch (DirectoryNotFoundException)
                {
                    // Ignore missing directories
                }
            }
            else if (File.Exists(path))
            {
                FileInfo? info = new FileInfo(path);
                root.Size = info.Length;
                root.ModifiedTime = info.LastWriteTime;
            }

            return root;
        }

        private bool CompareNodes(FileSystemNode? left, FileSystemNode? right, List<ComparisonItem> result, string indent)
        {
            bool hasDifferences = false;
            SortedSet<string> names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect all child names
            if (left != null)
                foreach (FileSystemNode? child in left.Children)
                {
                    names.Add(child.Name);
                }
            if (right != null)
                foreach (FileSystemNode? child in right.Children)
                {
                    names.Add(child.Name);
                }

            // Process each child
            foreach (string? name in names)
            {
                FileSystemNode? leftNode = left?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                FileSystemNode? rightNode = right?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                ComparisonStatus status = DetermineStatus(leftNode, rightNode);

                // Create comparison item
                ComparisonItem? item = new()
                {
                    Status = status,
                    TreeIndent = indent + (leftNode?.IsDirectory ?? rightNode?.IsDirectory ?? false ? "📁 " : "📄 ") + name,
                    Left = leftNode,
                    Right = rightNode
                };

                result.Add(item);

                // If the current item is not identical, mark differences
                if (status != ComparisonStatus.Identical)
                {
                    hasDifferences = true;
                }

                // Recursively compare subdirectories and track differences
                if ((leftNode?.IsDirectory ?? false) || (rightNode?.IsDirectory ?? false))
                {
                    bool childHasDifferences = CompareNodes(leftNode, rightNode, result, indent + "    ");
                    if (childHasDifferences)
                    {
                        hasDifferences = true;
                    }
                }
            }

            // Update the parent directory's status if necessary
            if ((left?.IsDirectory ?? false) || (right?.IsDirectory ?? false))
            {
                ComparisonItem? parentItem = result.LastOrDefault(i => i.Left == left || i.Right == right);
                if (parentItem != null && hasDifferences)
                {
                    parentItem.Status = ComparisonStatus.Modified;
                }
            }

            return hasDifferences;
        }

        private ComparisonStatus DetermineStatus(FileSystemNode? left, FileSystemNode? right)
        {
            if (left == null && right != null)
                return ComparisonStatus.RightOnly;

            if (left != null && right == null)
                return ComparisonStatus.LeftOnly;

            if (left != null && right != null)
            {
                // Check if types match (directory vs file)
                if (left.IsDirectory != right.IsDirectory)
                    return ComparisonStatus.Conflict;

                if (left.IsDirectory && right.IsDirectory)
                {
                    // Directory status will be updated in CompareNodes based on children
                    return ComparisonStatus.Identical; // Placeholder
                }
                else
                {
                    // File comparison
                    if (left.Size == right.Size &&
                        Math.Abs((left.ModifiedTime - right.ModifiedTime).TotalSeconds) < 1)
                        return ComparisonStatus.Identical;
                    else
                        return ComparisonStatus.Modified;
                }
            }

            return ComparisonStatus.Unknown;
        }
    }
}
