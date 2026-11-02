using dCompare.Common;
using dCompare.FileOperation.Models;
using dCompare.FileOperation.Strategies;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace dCompare.FileOperation
{
    /// <summary>
    /// Manages background file and directory operations with progress and cancellation.
    /// </summary>
    public class FileManager : IFileManager
    {
        private readonly IFileOperationStrategy _strategy;
        private bool _isPaused;

        // The Pause/Cancel buttons live on a different control from the one that starts most transfers, so the
        // stop request is owned here: Cancel() stops every batch this manager is running, whoever started it.
        private readonly object _cancelGate = new();
        private CancellationTokenSource _cancelSource = new();
        public Guid InstanceId { get; } = Guid.NewGuid();

        public FileManager(IFileOperationStrategy? strategy = null)
        {
            _strategy = strategy ?? new Kernel32FileCopyStrategy();
            _isPaused = false;
        }

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;

        public void Cancel()
        {
            lock (_cancelGate)
            {
                _cancelSource.Cancel();
            }
        }

        #region Public API Methods
        public void WhoAmI()
        {
            Log.Information($"FileManager ({InstanceId}) is performing an operation with data: ");
        }
        public Task<OperationResult> CopyAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            List<(string, string, SourceType)> items = [(source, destination, SourceType.Left)];
            return ProcessItemsAsync(items, FileOperationTypes.Copy, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> MoveAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            List<(string, string, SourceType)> items = [(source, destination, SourceType.Left)];
            return ProcessItemsAsync(items, FileOperationTypes.Move, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> DeleteAsync(string path, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            List<(string, string, SourceType)> items = [(path, string.Empty, SourceType.Left)];
            return ProcessItemsAsync(items, FileOperationTypes.Delete, false, progress, cancellationToken);
        }

        public Task<OperationResult> CopyMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return ProcessItemsAsync(items, FileOperationTypes.Copy, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> MoveMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return ProcessItemsAsync(items, FileOperationTypes.Move, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> DeleteMultipleAsync(List<string> paths, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var items = paths.ConvertAll(p => (p, string.Empty, SourceType.Left));
            return ProcessItemsAsync(items, FileOperationTypes.Delete, false, progress, cancellationToken);
        }

        #endregion

        private async Task<OperationResult> ProcessItemsAsync(List<(string source, string target, SourceType type)> items, FileOperationTypes opType, bool overwrite, IProgress<OperationProgress>? progress, CancellationToken callerToken)
        {
            // Both stop requests — the caller's token and the shared Pause/Cancel buttons — reach the loops
            // below through this one token.
            using CancellationTokenSource batchCts = CreateBatchTokenSource(callerToken);
            CancellationToken cancellationToken = batchCts.Token;

            Stopwatch stopwatch = Stopwatch.StartNew();
            int successCount = 0;
            List<string> failedItems = new();
            int totalCount = items.Count;
            int processedCount = 0;
            int lastReportedProgress = -1;

            void ReportProgress(int percentage, string currentItem)
            {
                if (progress != null && percentage > lastReportedProgress)
                {
                    progress.Report(new OperationProgress(percentage, currentItem));
                    lastReportedProgress = percentage;
                }
            }

            foreach (var (source, target, _) in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (_isPaused && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }

                string currentItem = source;

                try
                {
                    switch (opType)
                    {
                        case FileOperationTypes.Copy:
                            await HandleCopyAsync(source, target, overwrite, totalCount, processedCount, ReportProgress, cancellationToken);
                            break;
                        case FileOperationTypes.Move:
                            await HandleMoveAsync(source, target, overwrite, totalCount, processedCount, ReportProgress, cancellationToken);
                            break;
                        case FileOperationTypes.Delete:
                            await HandleDeleteAsync(source, totalCount, processedCount, ReportProgress, cancellationToken);
                            break;
                    }
                    successCount++;
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    // CopyFileEx answers a cancel with Win32 1235, not an OperationCanceledException; normalise
                    // it so the caller books the item as cancelled instead of quietly as failed.
                    failedItems.Add(source);
                    throw new OperationCanceledException($"{source} was cancelled.", ex, cancellationToken);
                }
                catch (Exception ex)
                {
                    failedItems.Add($"{source}: {ex.Message}");
                }
                finally
                {
                    processedCount++;
                    if (totalCount > 1)
                    {
                        ReportProgress(processedCount * 100 / totalCount, currentItem);
                    }
                }
            }

            stopwatch.Stop();
            ReportProgress(100, string.Empty);
            return new OperationResult(successCount, failedItems.Count, failedItems, stopwatch.Elapsed);
        }

        // A cancelled source is never reused: CancellationTokenSource cannot be un-cancelled. The old instance
        // is dropped without disposal because a batch that is still winding down may hold a registration on it.
        private CancellationTokenSource CreateBatchTokenSource(CancellationToken callerToken)
        {
            lock (_cancelGate)
            {
                if (_cancelSource.IsCancellationRequested)
                {
                    _cancelSource = new CancellationTokenSource();
                }
                return CancellationTokenSource.CreateLinkedTokenSource(callerToken, _cancelSource.Token);
            }
        }

        private async Task HandleCopyAsync(string source, string target, bool overwrite, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
        {
            bool isDirectory = Directory.Exists(source);
            if (isDirectory)
            {
                await CopyDirectoryAsync(source, target, overwrite, totalCount, processedCount, reportProgress, cancellationToken);
            }
            else
            {
                if (totalCount == 1)
                {
                    //long fileSize = new FileInfo(source).Length;
                    CopyProgressCallback byteProgressCallback = (totalFileSize, totalBytesTransferred, _, _, _, _, _, _, _) =>
                    {
                        if (totalFileSize > 0)
                        {
                            int percentage = (int)(totalBytesTransferred * 100 / totalFileSize);
                            reportProgress(percentage, source);
                        }
                        return cancellationToken.IsCancellationRequested ? CopyProgressResult.PROGRESS_CANCEL : CopyProgressResult.PROGRESS_CONTINUE;
                    };
                    await _strategy.CopyFileAsync(source, target, overwrite, byteProgressCallback, cancellationToken);
                }
                else
                {
                    int basePercentage = processedCount * 100 / totalCount;
                    reportProgress(basePercentage, source);
                    await _strategy.CopyFileAsync(source, target, overwrite, null, cancellationToken);
                }
            }
        }

        private async Task CopyDirectoryAsync(string sourceDir, string targetDir, bool overwrite, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
        {
            // Create target directory
            Directory.CreateDirectory(targetDir);
            //DirectoryInfo sourceInfo = new(sourceDir);
            //DirectoryInfo targetInfo = new(targetDir)
            //{
            //    CreationTimeUtc = sourceInfo.CreationTimeUtc,
            //    LastAccessTimeUtc = sourceInfo.LastAccessTimeUtc,
            //    LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc
            //};

            // Get all files and subdirectories
            string[] files = Directory.GetFiles(sourceDir);
            string[] subDirs = GetChildDirectories(sourceDir);
            int totalItems = files.Length + subDirs.Length;
            int processedItems = 0;

            // Copy files
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                //long fileSize = new FileInfo(file).Length;

                CopyProgressCallback byteProgressCallback = (totalFileSize, totalBytesTransferred, _, _, _, _, _, _, _) =>
                {
                    if (totalFileSize > 0)
                    {
                        int filePercentage = (int)(totalBytesTransferred * 100 / totalFileSize);
                        int overallPercentage = (int)(processedCount * 100 / totalCount + (filePercentage * (1 / (float)totalCount) / totalItems));
                        reportProgress(overallPercentage, file);
                    }
                    return cancellationToken.IsCancellationRequested ? CopyProgressResult.PROGRESS_CANCEL : CopyProgressResult.PROGRESS_CONTINUE;
                };

                await _strategy.CopyFileAsync(file, destFile, overwrite, byteProgressCallback, cancellationToken);
                processedItems++;
            }

            // Recursively copy subdirectories
            foreach (string subDir in subDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string dirName = Path.GetFileName(subDir);
                string destDir = Path.Combine(targetDir, dirName);
                await CopyDirectoryAsync(subDir, destDir, overwrite, totalCount, processedCount, reportProgress, cancellationToken);
                processedItems++;
            }
        }

        private async Task HandleMoveAsync(string source, string target, bool overwrite, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
        {
            bool isDirectory = Directory.Exists(source);
            if (isDirectory)
            {
                await MoveDirectoryAsync(source, target, overwrite, totalCount, processedCount, reportProgress, cancellationToken);
            }
            else
            {
                int basePercentage = processedCount * 100 / totalCount;
                reportProgress(basePercentage, source);
                _strategy.MoveFile(source, target, overwrite);
            }
        }

        private async Task MoveDirectoryAsync(string sourceDir, string targetDir, bool overwrite, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(targetDir);

            // Get all files and subdirectories
            string[] files = Directory.GetFiles(sourceDir);
            string[] subDirs = GetChildDirectories(sourceDir);
            int totalItems = files.Length + subDirs.Length;
            int processedItems = 0;

            // Move files
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                int overallPercentage = (int)(processedCount * 100 / totalCount + (processedItems * (1 / (float)totalCount) / totalItems));
                reportProgress(overallPercentage, file);
                _strategy.MoveFile(file, destFile, overwrite);
                processedItems++;
            }

            // Recursively move subdirectories
            foreach (string subDir in subDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string dirName = Path.GetFileName(subDir);
                string destDir = Path.Combine(targetDir, dirName);
                await MoveDirectoryAsync(subDir, destDir, overwrite, totalCount, processedCount, reportProgress, cancellationToken);
                processedItems++;
            }

            // Delete the source directory if empty
            if (Directory.GetFiles(sourceDir).Length == 0 && Directory.GetDirectories(sourceDir).Length == 0)
            {
                Directory.Delete(sourceDir);
            }
        }

        private async Task HandleDeleteAsync(string path, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int basePercentage = processedCount * 100 / totalCount;
            reportProgress(basePercentage, path);

            if (Directory.Exists(path))
            {
                // One shell operation for the whole tree, so the folder lands in the Recycle Bin as a single
                // restorable entry instead of one entry per child. A tree containing a junction or directory
                // symlink keeps the walk below: the shell may follow the link and delete files that live
                // outside the compared tree, and the walk refuses to delete a directory that still holds one.
                if (ContainsReparsePoint(path) || !RecycleBin.TryDelete(path, isDirectory: true))
                {
                    await DeleteDirectoryAsync(path, totalCount, processedCount, reportProgress, cancellationToken);
                }
            }
            else
            {
                // The Recycle Bin first so a mis-click stays undoable. The strategy delete stays unconditional:
                // it is the permanent fallback for locations without a bin, and File.Delete on a path the shell
                // already moved away is a documented no-op.
                RecycleBin.TryDelete(path, isDirectory: false);
                _strategy.DeleteFile(path);
            }
        }

        private async Task DeleteDirectoryAsync(string dirPath, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
        {
            // Get all files and subdirectories
            string[] files = Directory.GetFiles(dirPath);
            string[] subDirs = GetChildDirectories(dirPath);
            int totalItems = files.Length + subDirs.Length;
            int processedItems = 0;

            // Delete files
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int overallPercentage = (int)(processedCount * 100 / totalCount + (processedItems * (1 / (float)totalCount) / totalItems));
                reportProgress(overallPercentage, file);
                _strategy.DeleteFile(file);
                processedItems++;
            }

            // Recursively delete subdirectories
            foreach (string subDir in subDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await DeleteDirectoryAsync(subDir, totalCount, processedCount, reportProgress, cancellationToken);
                processedItems++;
            }

            // Delete the directory itself
            _strategy.DeleteDirectory(dirPath);
        }

        // Junctions and directory symlinks are never walked: a link back to an ancestor overflows the stack
        // (kills the process, uncatchable) and a link pointing outside the compared tree would be copied or
        // deleted as if it belonged to it. A skipped link also keeps a moved source folder from being
        // deleted, which is the safe outcome.
        private static string[] GetChildDirectories(string dir) =>
            Directory.GetDirectories(dir).Where(d => !FileExtensions.IsReparsePoint(d)).ToArray();

        // An unreadable subtree counts as containing a reparse point: the conservative permanent walk is
        // taken instead of handing the whole tree to the shell.
        private static bool ContainsReparsePoint(string dir)
        {
            try
            {
                return Directory.GetDirectories(dir).Any(d => FileExtensions.IsReparsePoint(d) || ContainsReparsePoint(d));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }
    }
}