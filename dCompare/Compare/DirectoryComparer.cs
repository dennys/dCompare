using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.DirTreeSystem;
using dCompare.DirTreeSystem.Models;
using Serilog;
using System.IO;
using System.IO.Hashing;
using System.Security.Cryptography;

namespace dCompare.Compare
{
    public interface IFileSystem
    {
        bool DirectoryExists(string path);
        bool FileExists(string path);
        IEnumerable<string> GetDirectories(string path);
        IEnumerable<string> GetFiles(string path);
        DateTime GetLastWriteTime(string path);
        FileInfo GetFileInfo(string path);
        Stream OpenRead(string path);
    }

    public interface IHashProvider
    {
        byte[] ComputeMD5(Stream stream, double ratio = 1.0);
        byte[] ComputeXxHash64(Stream stream, double ratio = 1.0);
    }

    public class FileSystem : IFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);
        public IEnumerable<string> GetDirectories(string path) => Directory.GetDirectories(path);
        public IEnumerable<string> GetFiles(string path) => Directory.GetFiles(path);
        public DateTime GetLastWriteTime(string path) => Directory.GetLastWriteTime(path);
        public FileInfo GetFileInfo(string path) => new(path);
        public Stream OpenRead(string path) => File.OpenRead(path);
    }

    public class HashProvider : IHashProvider
    {
        public byte[] ComputeMD5(Stream stream, double ratio = 1.0)
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

        public byte[] ComputeXxHash64(Stream stream, double ratio = 1.0)
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

    public class DirectoryComparer
    {
        private readonly IFileSystem _fileSystem;
        private readonly IHashProvider _hashProvider;
        private readonly bool _parallel;
        private readonly Dictionary<ComparisonAlgorithm, ComparisonAlgorithmInfo> _algorithms;

        public DirectoryComparer(AppConfiguration? config = null, IFileSystem? fileSystem = null, IHashProvider? hashProvider = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            _hashProvider = hashProvider ?? new HashProvider();
            _parallel = config?.Options?.Compare?.ScanMethod?.ParallelOptions ?? true;
            _algorithms = config?.Options?.Compare?.Algorithms ?? ComparisonAlgorithms.AlgorithmSettings;
        }

        public class ComparisonItem
        {
            public ComparisonStatus Status { get; set; }
            public string TreeIndent { get; set; } = "";
            public FileSystemNode? Left { get; set; }
            public FileSystemNode? Right { get; set; }
        }

        public async Task<List<ComparisonItem>> CompareAsync(
            string leftPath,
            string rightPath,
            CancellationToken cancellationToken = default,
            Action<double>? progressCallback = null,
            ComparisonAlgorithm algorithm = ComparisonAlgorithm.ModifiedDateAndSize)
        {
            FileSystemNode? leftRoot = null;
            FileSystemNode? rightRoot = null;

            int totalDirs = 0;
            if (!string.IsNullOrEmpty(leftPath) && (Directory.Exists(leftPath) || FileExtensions.IsArchiveFile(leftPath)))
            {
                totalDirs += CountDirectories(leftPath);
            }
            if (!string.IsNullOrEmpty(rightPath) && (Directory.Exists(rightPath) || FileExtensions.IsArchiveFile(rightPath)))
            {
                totalDirs += CountDirectories(rightPath);
            }

            int processedDirs = 0;
            int lastReportedTenth = -1;
            double progressPerDir = totalDirs > 0 ? 1.0 / totalDirs : 1.0;

            // Both tree builders report from their own worker thread, so the throttle state has to be shared atomically.
            Action updateProgress = () =>
            {
                int currentCount = Interlocked.Increment(ref processedDirs);
                int tenth = (int)Math.Floor(currentCount * progressPerDir * 10);
                int seen = Volatile.Read(ref lastReportedTenth);
                if (tenth > seen && Interlocked.CompareExchange(ref lastReportedTenth, tenth, seen) == seen)
                {
                    progressCallback?.Invoke(tenth / 10.0);
                }
            };

            if (_parallel)
            {
                var tasks = new List<Task>();
                // If one side throws, Task.WhenAll only surfaces that one exception and the sibling builder keeps
                // walking the disk; the caller disposes its CTS in finally, so Cancel stops working. Linking lets
                // us stop the sibling ourselves.
                using CancellationTokenSource scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (!string.IsNullOrEmpty(leftPath))
                {
                    tasks.Add(Task.Run(() =>
                    {
                        var leftTreeBuilder = FileSystemTreeBuilderFactory.Create(leftPath, _fileSystem);
                        leftRoot = leftTreeBuilder.BuildTreeRecursive(leftPath, 0, scanCts.Token, updateProgress);
                    }, scanCts.Token));
                }

                if (!string.IsNullOrEmpty(rightPath))
                {
                    tasks.Add(Task.Run(() =>
                    {
                        var rightTreeBuilder = FileSystemTreeBuilderFactory.Create(rightPath, _fileSystem);
                        rightRoot = rightTreeBuilder.BuildTreeRecursive(rightPath, 0, scanCts.Token, updateProgress);
                    }, scanCts.Token));
                }

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch
                {
                    scanCts.Cancel();
                    throw;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(leftPath))
                {
                    var leftTreeBuilder = FileSystemTreeBuilderFactory.Create(leftPath, _fileSystem);
                    leftRoot = leftTreeBuilder.BuildTreeRecursive(leftPath, 0, cancellationToken, updateProgress);
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(rightPath))
                {
                    var rightTreeBuilder = FileSystemTreeBuilderFactory.Create(rightPath, _fileSystem);
                    rightRoot = rightTreeBuilder.BuildTreeRecursive(rightPath, 0, cancellationToken, updateProgress);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();

            List<ComparisonItem> result = new();

            Log.Information("Start to create ComparisonItem.");
            ComparisonItem rootItem = new()
            {
                Status = ComparisonStatus.Identical,
                Left = leftRoot,
                Right = rightRoot,
                TreeIndent = ""
            };
            result.Add(rootItem);

            Log.Information("Start to compare nodes.");
            CompareNodes(leftRoot, rightRoot, result, "", cancellationToken, algorithm, rootItem);

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
            bool leftIsFile = _fileSystem.FileExists(leftSubPath);
            bool rightIsFile = _fileSystem.FileExists(rightSubPath);
            bool leftIsDir = _fileSystem.DirectoryExists(leftSubPath);
            bool rightIsDir = _fileSystem.DirectoryExists(rightSubPath);

            // Build FileSystemNode trees for the paths
            var leftTreeBuilder = FileSystemTreeBuilderFactory.Create(leftSubPath, _fileSystem);
            var rightTreeBuilder = FileSystemTreeBuilderFactory.Create(rightSubPath, _fileSystem);

            FileSystemNode? leftSubRoot = leftIsFile || leftIsDir ? leftTreeBuilder.BuildTreeRecursive(leftSubPath, subDirDepth, cancellationToken) : null;
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemNode? rightSubRoot = rightIsFile || rightIsDir ? rightTreeBuilder.BuildTreeRecursive(rightSubPath, subDirDepth, cancellationToken) : null;
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
                CompareNodes(leftSubRoot, rightSubRoot, result, "    ", cancellationToken, algorithm, subPathItem);
            }

            return result;
        }

        private int CountDirectories(string path)
        {
            if (File.Exists(path) && FileExtensions.IsArchiveFile(path))
            {
                return 1;
            }

            if (!_fileSystem.DirectoryExists(path)) return 0;
            int count = 1;
            try
            {
                foreach (string dir in _fileSystem.GetDirectories(path))
                {
                    if (FileExtensions.IsReparsePoint(dir)) continue;
                    count++;
                    count += CountDirectories(dir);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
            return count;
        }

        /// <summary>
        /// Compares two file system nodes and their children, and adds the differences to the result list.
        /// </summary>
        /// <param name="left">The left file system node to compare.</param>
        /// <param name="right">The right file system node to compare.</param>
        /// <param name="result">The list that will be populated with comparison results.</param>
        /// <param name="indent">The indentation string used for formatting nested comparisons.</param>
        /// <param name="cancellationToken">A token used to monitor for cancellation requests.</param>
        /// <param name="algorithm">The comparison algorithm to use.</param>
        /// <returns>
        /// <c>true</c> if there are content differences between the nodes; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the operation is canceled via the <paramref name="cancellationToken"/>.
        /// </exception>
        private bool CompareNodes(FileSystemNode? left, FileSystemNode? right, List<ComparisonItem> result,
            string indent, CancellationToken cancellationToken, ComparisonAlgorithm algorithm, ComparisonItem? parentItem)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool hasContentDifferences = false;
            SortedSet<string> names = new(StringComparer.OrdinalIgnoreCase);

            if (left != null)
                foreach (FileSystemNode child in left.Children)
                {
                    names.Add(child.Name);
                }
            if (right != null)
                foreach (FileSystemNode child in right.Children)
                {
                    names.Add(child.Name);
                }

            foreach (string name in names)
            {
                // Must throw, not return: a half-scanned tree reported as "no differences" invites the user to delete good files.
                cancellationToken.ThrowIfCancellationRequested();

                FileSystemNode? leftNode = left?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                FileSystemNode? rightNode = right?.Children.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                ComparisonStatus status = DetermineStatus(leftNode, rightNode, algorithm);

                ComparisonItem item = new()
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
                    bool childHasDifferences = CompareNodes(leftNode, rightNode, result, indent + "    ", cancellationToken, algorithm, item);
                    if (childHasDifferences)
                    {
                        hasContentDifferences = true;
                    }
                }
            }

            // The parent is marked through the reference we already hold. The previous
            // result.LastOrDefault(i => i.Left == left || i.Right == right) matched every RightOnly row whenever
            // left was null, so it repainted the deepest descendant "Modified" and left the real parent untouched.
            if (parentItem != null && hasContentDifferences &&
                (parentItem.Status == ComparisonStatus.Identical || parentItem.Status == ComparisonStatus.Unknown))
            {
                parentItem.Status = ComparisonStatus.Modified;
            }

            return hasContentDifferences;
        }

        // Streamed so "byte-by-byte" works on files bigger than available RAM; File.ReadAllBytes did not.
        private bool StreamsEqual(string leftPath, string rightPath)
        {
            const int bufferSize = 8192;
            using Stream leftStream = _fileSystem.OpenRead(leftPath);
            using Stream rightStream = _fileSystem.OpenRead(rightPath);
            byte[] leftBuffer = new byte[bufferSize];
            byte[] rightBuffer = new byte[bufferSize];

            while (true)
            {
                int leftRead = ReadFull(leftStream, leftBuffer);
                int rightRead = ReadFull(rightStream, rightBuffer);
                if (leftRead != rightRead)
                    return false;
                if (leftRead == 0)
                    return true;
                if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
                    return false;
            }
        }

        private static int ReadFull(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                    break;
                total += read;
            }
            return total;
        }

        private ComparisonStatus DetermineStatus(FileSystemNode? left, FileSystemNode? right, ComparisonAlgorithm algorithm)
        {
            Log.Debug($"Use {algorithm} to compare {left?.FullPath} and {right?.FullPath}");

            // "Partial" hashes only the configured fraction of each file; anything missing or out of range is a full read.
            double ratio = _algorithms.TryGetValue(algorithm, out ComparisonAlgorithmInfo? info) && info?.ComparisonPercentage is > 0 and <= 100
                ? info.ComparisonPercentage!.Value / 100.0
                : 1.0;

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
                                if (left.Size != right.Size)
                                    return ComparisonStatus.Modified;
                                return StreamsEqual(left.FullPath, right.FullPath)
                                    ? ComparisonStatus.Identical
                                    : ComparisonStatus.Modified;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Error comparing file contents: {ex.Message}");
                                return ComparisonStatus.Conflict;
                            }

                        case ComparisonAlgorithm.MD5:
                            try
                            {
                                using Stream leftStream = _fileSystem.OpenRead(left.FullPath);
                                using Stream rightStream = _fileSystem.OpenRead(right.FullPath);

                                byte[] leftHash = _hashProvider.ComputeMD5(leftStream, ratio);
                                byte[] rightHash = _hashProvider.ComputeMD5(rightStream, ratio);
                                return leftHash.SequenceEqual(rightHash)
                                    ? ComparisonStatus.Identical
                                    : ComparisonStatus.Modified;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Error computing MD5: {ex.Message}");
                                return ComparisonStatus.Conflict;
                            }
                        case ComparisonAlgorithm.xxHash64_Full:
                        case ComparisonAlgorithm.xxHash64_Partial:
                            try
                            {
                                using Stream leftStream = _fileSystem.OpenRead(left.FullPath);
                                using Stream rightStream = _fileSystem.OpenRead(right.FullPath);

                                byte[] leftHash = _hashProvider.ComputeXxHash64(leftStream, ratio);
                                byte[] rightHash = _hashProvider.ComputeXxHash64(rightStream, ratio);
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
    }
}