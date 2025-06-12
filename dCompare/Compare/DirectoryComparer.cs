using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;
using System.Threading;

namespace dCompare.Compare
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

        public List<ComparisonItem> Compare(string leftPath, string rightPath, CancellationToken cancellationToken = default, Action<double> progressCallback = null)
        {
            Log.Information("Start to compare.");
            Log.Information("Start to build left path.");

            // Count total directories for progress
            int totalDirs = CountDirectories(leftPath) + CountDirectories(rightPath);
            int processedDirs = 0;
            double progressPerDir = totalDirs > 0 ? 1.0 / totalDirs : 1.0;
            double lastReportedProgress = -0.1; // Ensure first 10% is reported

            FileSystemNode? leftRoot = BuildTree(leftPath, 0, cancellationToken, () =>
            {
                processedDirs++;
                double currentProgress = processedDirs * progressPerDir;
                // Report progress only at 10% increments
                if (currentProgress >= lastReportedProgress + 0.1)
                {
                    progressCallback?.Invoke(currentProgress);
                    lastReportedProgress = Math.Floor(currentProgress * 10) / 10;
                }
            });
            cancellationToken.ThrowIfCancellationRequested();

            Log.Information("Start to build right path.");
            FileSystemNode? rightRoot = BuildTree(rightPath, 0, cancellationToken, () =>
            {
                processedDirs++;
                double currentProgress = processedDirs * progressPerDir;
                // Report progress only at 10% increments
                if (currentProgress >= lastReportedProgress + 0.1)
                {
                    progressCallback?.Invoke(currentProgress);
                    lastReportedProgress = Math.Floor(currentProgress * 10) / 10;
                }
            });
            cancellationToken.ThrowIfCancellationRequested();

            List<ComparisonItem> result = new();

            Log.Information("Start to create ComparisonItem.");
            ComparisonItem? rootItem = new ComparisonItem
            {
                Status = ComparisonStatus.Identical,
                Left = leftRoot,
                Right = rightRoot,
                TreeIndent = ""
            };
            result.Add(rootItem);

            Log.Information("Start to compare.");
            bool hasDifferences = CompareNodes(leftRoot, rightRoot, result, "", cancellationToken);

            Log.Information("Start to show result.");
            if (hasDifferences)
            {
                rootItem.Status = ComparisonStatus.Modified;
            }

            // Ensure 100% is reported at completion
            progressCallback?.Invoke(1.0);

            Log.Information("End to compare.");
            return result;
        }

        private int CountDirectories(string path)
        {
            if (!Directory.Exists(path)) return 0;
            int count = 1; // Include the root directory
            try
            {
                foreach (string dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
                {
                    count++;
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            return count;
        }

        private FileSystemNode BuildTree(string path, int depth, CancellationToken cancellationToken, Action onDirectoryProcessed = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                long totalSize = 0;

                try
                {
                    foreach (string? dir in Directory.GetDirectories(path))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FileSystemNode childDir = BuildTree(dir, depth + 1, cancellationToken, onDirectoryProcessed);
                        root.Children.Add(childDir);
                        totalSize += childDir.Size;
                        onDirectoryProcessed?.Invoke();
                    }

                    foreach (string? file in Directory.GetFiles(path))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FileSystemNode childFile = BuildTree(file, depth + 1, cancellationToken, onDirectoryProcessed);
                        root.Children.Add(childFile);
                        totalSize += childFile.Size;
                    }

                    root.Size = totalSize;
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (DirectoryNotFoundException)
                {
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

        private bool CompareNodes(FileSystemNode? left, FileSystemNode? right, List<ComparisonItem> result, string indent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasDifferences = false;
            SortedSet<string> names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

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

            foreach (string? name in names)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileSystemNode? leftNode = left?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                FileSystemNode? rightNode = right?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                ComparisonStatus status = DetermineStatus(leftNode, rightNode);

                ComparisonItem? item = new()
                {
                    Status = status,
                    TreeIndent = indent + (leftNode?.IsDirectory ?? rightNode?.IsDirectory ?? false ? "📁 " : "📄 ") + name,
                    Left = leftNode,
                    Right = rightNode
                };

                result.Add(item);

                if (status != ComparisonStatus.Identical)
                {
                    hasDifferences = true;
                }

                if ((leftNode?.IsDirectory ?? false) || (rightNode?.IsDirectory ?? false))
                {
                    bool childHasDifferences = CompareNodes(leftNode, rightNode, result, indent + "    ", cancellationToken);
                    if (childHasDifferences)
                    {
                        hasDifferences = true;
                    }
                }
            }

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
                if (left.IsDirectory != right.IsDirectory)
                    return ComparisonStatus.Conflict;

                if (left.IsDirectory && right.IsDirectory)
                {
                    return ComparisonStatus.Identical;
                }
                else
                {
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