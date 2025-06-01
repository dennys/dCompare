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

            // 添加根目錄的比較
            result.Add(new ComparisonItem
            {
                Status = ComparisonStatus.Identical,
                Left = leftRoot,
                Right = rightRoot,
                TreeIndent = ""
            });

            CompareNodes(leftRoot, rightRoot, result, "");
            return result;
        }

        private FileSystemNode BuildTree(string path, int depth)
        {
            var root = new FileSystemNode
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
                    // 先處理子目錄
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        root.Children.Add(BuildTree(dir, depth + 1));
                    }

                    // 再處理檔案
                    foreach (var file in Directory.GetFiles(path))
                    {
                        root.Children.Add(BuildTree(file, depth + 1));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // 如果沒有權限存取，忽略該目錄
                }
                catch (DirectoryNotFoundException)
                {
                    // 目錄不存在，忽略
                }
            }
            else if (File.Exists(path))
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

            // 收集所有子項目名稱
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

                var status = DetermineStatus(leftNode, rightNode);

                var item = new ComparisonItem
                {
                    Status = status,
                    TreeIndent = indent + (leftNode?.IsDirectory ?? rightNode?.IsDirectory ?? false ? "📁 " : "📄 ") + name,
                    Left = leftNode,
                    Right = rightNode
                };

                result.Add(item);

                // 如果是資料夾，遞迴比較子節點
                if ((leftNode?.IsDirectory ?? false) || (rightNode?.IsDirectory ?? false))
                {
                    CompareNodes(leftNode, rightNode, result, indent + "    ");
                }
            }
        }

        private ComparisonStatus DetermineStatus(FileSystemNode? left, FileSystemNode? right)
        {
            if (left == null && right != null)
                return ComparisonStatus.RightOnly;

            if (left != null && right == null)
                return ComparisonStatus.LeftOnly;

            if (left != null && right != null)
            {
                // 檢查類型是否匹配（目錄vs檔案）
                if (left.IsDirectory != right.IsDirectory)
                    return ComparisonStatus.Conflict;

                if (left.IsDirectory && right.IsDirectory)
                {
                    // 目錄比較，暫時標記為相同，實際狀態會由子項目決定
                    return ComparisonStatus.Identical;
                }
                else
                {
                    // 檔案比較
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
