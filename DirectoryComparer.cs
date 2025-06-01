using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var leftRoot = BuildTree(leftPath, 0);
            var rightRoot = BuildTree(rightPath, 0);
            var result = new List<ComparisonItem>();
            CompareNodes(leftRoot, rightRoot, result, "");
            return result;
        }

        private FileSystemNode BuildTree(string path, int depth)
        {
            var root = new FileSystemNode
            {
                Name = System.IO.Path.GetFileName(path),
                FullPath = path,
                IsDirectory = Directory.Exists(path),
                Depth = depth
            };

            if (root.IsDirectory)
            {
                root.ModifiedTime = Directory.GetLastWriteTime(path);
                foreach (var dir in Directory.GetDirectories(path))
                {
                    root.Children.Add(BuildTree(dir, depth + 1));
                }
                foreach (var file in Directory.GetFiles(path))
                {
                    root.Children.Add(BuildTree(file, depth + 1));
                }
            }
            else
            {
                var info = new FileInfo(path);
                root.Size = info.Length;
                root.ModifiedTime = info.LastWriteTime;
            }

            return root;
        }

        private void CompareNodes(FileSystemNode? left, FileSystemNode? right, List<ComparisonItem> result, string indent)
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (left != null)
                foreach (var child in left.Children)
                    names.Add(child.Name);
            if (right != null)
                foreach (var child in right.Children)
                    names.Add(child.Name);

            foreach (var name in names)
            {
                var leftNode = left?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                var rightNode = right?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                var status = ComparisonStatus.Unknown;

                if (leftNode != null && rightNode != null)
                {
                    if (leftNode.IsDirectory && rightNode.IsDirectory)
                    {
                        status = ComparisonStatus.Identical; // 先假設資料夾相同，之後可深入比較子項
                    }
                    else if (!leftNode.IsDirectory && !rightNode.IsDirectory)
                    {
                        if (leftNode.Size == rightNode.Size && Math.Abs((leftNode.ModifiedTime - rightNode.ModifiedTime).TotalSeconds) < 1)
                            status = ComparisonStatus.Identical;
                        else
                            status = ComparisonStatus.Modified;
                    }
                    else
                    {
                        status = ComparisonStatus.Conflict;
                    }
                }
                else if (leftNode != null)
                {
                    status = ComparisonStatus.LeftOnly;
                }
                else if (rightNode != null)
                {
                    status = ComparisonStatus.RightOnly;
                }

                result.Add(new ComparisonItem
                {
                    Status = status,
                    TreeIndent = indent + (leftNode?.IsDirectory ?? rightNode?.IsDirectory ?? false ? "📁 " : "  ") + name,
                    Left = leftNode,
                    Right = rightNode
                });

                // 如果是資料夾，遞迴比較子節點
                if ((leftNode?.IsDirectory ?? false) || (rightNode?.IsDirectory ?? false))
                {
                    CompareNodes(leftNode, rightNode, result, indent + "    ");
                }
            }
        }
    }
}
