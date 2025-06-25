using dCompare.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Security.Cryptography;
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

        public List<ComparisonItem> Compare(string leftPath, string rightPath, CancellationToken cancellationToken = default,
    Action<double>? progressCallback = null, ComparisonAlgorithm algorithm = ComparisonAlgorithm.ModifiedDateAndSize)
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
            bool hasDifferences = CompareNodes(leftRoot, rightRoot, result, "", cancellationToken, algorithm);

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

        public List<ComparisonItem> CompareSubtree(string leftRootPath, string rightRootPath, string subDirectoryRelativePath,
            CancellationToken cancellationToken, ComparisonAlgorithm algorithm)
        {
            string leftSubPath = Path.Combine(leftRootPath, subDirectoryRelativePath);
            string rightSubPath = Path.Combine(rightRootPath, subDirectoryRelativePath);

            // Calculate the initial depth of the path being compared
            int subDirDepth = string.IsNullOrEmpty(subDirectoryRelativePath)
                ? 0
                : subDirectoryRelativePath.Count(c => c == Path.DirectorySeparatorChar);

            cancellationToken.ThrowIfCancellationRequested();

            // Check if paths are files or directories
            bool leftIsFile = File.Exists(leftSubPath);
            bool rightIsFile = File.Exists(rightSubPath);
            bool leftIsDir = Directory.Exists(leftSubPath);
            bool rightIsDir = Directory.Exists(rightSubPath);

            // Build FileSystemNode trees for the paths
            FileSystemNode? leftSubRoot = leftIsFile || leftIsDir ? BuildTree(leftSubPath, subDirDepth, cancellationToken) : null;
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemNode? rightSubRoot = rightIsFile || rightIsDir ? BuildTree(rightSubPath, subDirDepth, cancellationToken) : null;
            cancellationToken.ThrowIfCancellationRequested();

            List<ComparisonItem> result = new();

            // Create the ComparisonItem for the path (file or directory)
            ComparisonItem subPathItem = new()
            {
                Status = DetermineStatus(leftSubRoot, rightSubRoot, algorithm),
                Left = leftSubRoot,
                Right = rightSubRoot,
                TreeIndent = (leftIsDir || rightIsDir) ? "📁 " + Path.GetFileName(subDirectoryRelativePath)
                    : "📄 " + Path.GetFileName(subDirectoryRelativePath)
            };

            // Add the item if the path exists on at least one side
            if (leftSubRoot != null || rightSubRoot != null)
            {
                result.Add(subPathItem);
            }

            // Compare children only if both paths are directories
            if (leftIsDir || rightIsDir)
            {
                bool childHasDifferences = CompareNodes(leftSubRoot, rightSubRoot, result, "    ", cancellationToken, algorithm);
                if (childHasDifferences)
                {
                    subPathItem.Status = ComparisonStatus.Modified;
                }
            }

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

        private FileSystemNode BuildTree(string path, int depth, CancellationToken cancellationToken, Action? onDirectoryProcessed = null)
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

        private bool CompareNodes(FileSystemNode? left, FileSystemNode? right, List<ComparisonItem> result,
            string indent, CancellationToken cancellationToken, ComparisonAlgorithm algorithm)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasContentDifferences = false; // Track content differences
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

                ComparisonStatus status = DetermineStatus(leftNode, rightNode, algorithm);

                ComparisonItem? item = new()
                {
                    Status = status,
                    TreeIndent = indent + (leftNode?.IsDirectory ?? rightNode?.IsDirectory ?? false ? "📁 " : "📄 ") + name,
                    Left = leftNode,
                    Right = rightNode
                };

                result.Add(item);

                // Mark as having differences for Modified, Conflict, LeftOnly, or RightOnly statuses
                if (status == ComparisonStatus.Modified ||
                    status == ComparisonStatus.Conflict ||
                    status == ComparisonStatus.LeftOnly ||
                    status == ComparisonStatus.RightOnly)
                {
                    hasContentDifferences = true;
                }

                if ((leftNode?.IsDirectory ?? false) || (rightNode?.IsDirectory ?? false))
                {
                    bool childHasDifferences = CompareNodes(leftNode, rightNode, result, indent + "    ", cancellationToken, algorithm);
                    if (childHasDifferences)
                    {
                        hasContentDifferences = true;
                    }
                }
            }

            if ((left?.IsDirectory ?? false) || (right?.IsDirectory ?? false))
            {
                ComparisonItem? parentItem = result.LastOrDefault(i => i.Left == left || i.Right == right);
                if (parentItem != null && hasContentDifferences)
                {
                    parentItem.Status = ComparisonStatus.Modified;
                }
            }

            return hasContentDifferences;
        }

        private ComparisonStatus DetermineStatus(FileSystemNode? left, FileSystemNode? right, ComparisonAlgorithm algorithm, double ratio = 1.0)
        {
            Log.Debug($"Use {algorithm} to compare {left?.FullPath} and {right?.FullPath}");
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
                    switch (algorithm)
                    {
                        case ComparisonAlgorithm.ModifiedDateAndSize:
                            if (left.Size == right.Size &&
                                Math.Abs((left.ModifiedTime - right.ModifiedTime).TotalSeconds) < 1)
                                return ComparisonStatus.Identical;
                            else
                                return ComparisonStatus.Modified;

                        case ComparisonAlgorithm.FullContents:
                            try
                            {
                                byte[] leftContent = File.ReadAllBytes(left.FullPath);
                                byte[] rightContent = File.ReadAllBytes(right.FullPath);
                                if (leftContent.SequenceEqual(rightContent))
                                    return ComparisonStatus.Identical;
                                else
                                    return ComparisonStatus.Modified;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Error comparing file contents: {ex.Message}");
                                return ComparisonStatus.Conflict;
                            }

                        case ComparisonAlgorithm.MD5:
                            try
                            {
                                using var leftStream = File.OpenRead(left.FullPath);
                                using var rightStream = File.OpenRead(right.FullPath);

                                var leftHash = ComputeMD5(leftStream, ratio);
                                var rightHash = ComputeMD5(rightStream, ratio);
                                return leftHash.SequenceEqual(rightHash)
                                    ? ComparisonStatus.Identical
                                    : ComparisonStatus.Modified;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Error computing MD5: {ex.Message}");
                                return ComparisonStatus.Conflict;
                            }
                        case ComparisonAlgorithm.xxHash64:
                            try
                            {
                                using var leftStream = File.OpenRead(left.FullPath);
                                using var rightStream = File.OpenRead(right.FullPath);

                                var leftHash = ComputeXxHash64(leftStream, ratio);
                                var rightHash = ComputeXxHash64(rightStream, ratio);
                                return leftHash.SequenceEqual(rightHash)
                                    ? ComparisonStatus.Identical
                                    : ComparisonStatus.Modified;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Error computing XxHash64: {ex.Message}");
                                return ComparisonStatus.Conflict;
                            }

                        default:
                            Log.Warning($"Unknown comparison algorithm: {algorithm}. Falling back to ModifiedDateAndSize.");
                            if (left.Size == right.Size &&
                                Math.Abs((left.ModifiedTime - right.ModifiedTime).TotalSeconds) < 1)
                                return ComparisonStatus.Identical;
                            else
                                return ComparisonStatus.Modified;
                    }
                }
            }

            return ComparisonStatus.Unknown;
        }

        private static byte[] ComputeMD5(Stream stream, double ratio = 1.0)
        {
            if (stream.CanSeek)
                stream.Position = 0;

            long lengthToRead = (long)(stream.Length * ratio);
            using var md5 = MD5.Create();
            byte[] buffer = new byte[8192];
            long totalRead = 0;

            while (totalRead < lengthToRead)
            {
                int toRead = (int)Math.Min(buffer.Length, lengthToRead - totalRead);
                int bytesRead = stream.Read(buffer, 0, toRead);
                if (bytesRead == 0) break;

                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                totalRead += bytesRead;
            }
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return md5.Hash!;
        }

        private static byte[] ComputeXxHash64(Stream stream, double ratio = 1.0)
        {
            if (stream.CanSeek)
                stream.Position = 0;

            long lengthToRead = (long)(stream.Length * ratio);
            var hasher = new XxHash64();
            byte[] buffer = new byte[8192];
            long totalRead = 0;

            while (totalRead < lengthToRead)
            {
                int toRead = (int)Math.Min(buffer.Length, lengthToRead - totalRead);
                int bytesRead = stream.Read(buffer, 0, toRead);
                if (bytesRead == 0) break;

                hasher.Append(buffer.AsSpan(0, bytesRead));
                totalRead += bytesRead;
            }
            return hasher.GetHashAndReset();
        }
    }
}