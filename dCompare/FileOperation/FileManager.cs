using dCompare.FileOperation.Strategies;
using dCompare.FileOperation.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using dCompare.Compare;

namespace dCompare.FileOperation
{
    /// <summary>
    /// Manages background file and directory operations with progress and cancellation.
    /// </summary>
    public class FileManager : IFileManager
    {
        private readonly IFileOperationStrategy _strategy;
        private bool _isPaused;

        public FileManager(IFileOperationStrategy? strategy = null)
        {
            _strategy = strategy ?? new Kernel32FileCopyStrategy();
            _isPaused = false;
        }

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;

        #region Public API Methods

        public Task<OperationResult> CopyAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var items = new List<(string, string, SourceType)> { (source, destination, SourceType.Left) };
            return ProcessItemsAsync(items, OperationType.Copy, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> MoveAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var items = new List<(string, string, SourceType)> { (source, destination, SourceType.Left) };
            return ProcessItemsAsync(items, OperationType.Move, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> DeleteAsync(string path, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var items = new List<(string, string, SourceType)> { (path, string.Empty, SourceType.Left) };
            return ProcessItemsAsync(items, OperationType.Delete, false, progress, cancellationToken);
        }

        public Task<OperationResult> CopyMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return ProcessItemsAsync(items, OperationType.Copy, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> MoveMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            return ProcessItemsAsync(items, OperationType.Move, overwrite, progress, cancellationToken);
        }

        public Task<OperationResult> DeleteMultipleAsync(List<string> paths, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var items = paths.ConvertAll(p => (p, string.Empty, SourceType.Left));
            return ProcessItemsAsync(items, OperationType.Delete, false, progress, cancellationToken);
        }

        #endregion

        private enum OperationType { Copy, Move, Delete }

        private async Task<OperationResult> ProcessItemsAsync(List<(string source, string target, SourceType type)> items, OperationType opType, bool overwrite, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            int successCount = 0;
            var failedItems = new List<string>();
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
                        case OperationType.Copy:
                            await HandleCopyAsync(source, target, overwrite, totalCount, processedCount, ReportProgress, cancellationToken);
                            break;
                        case OperationType.Move:
                            await HandleMoveAsync(source, target, overwrite, totalCount, processedCount, ReportProgress, cancellationToken);
                            break;
                        case OperationType.Delete:
                            await HandleDeleteAsync(source, totalCount, processedCount, ReportProgress, cancellationToken);
                            break;
                    }
                    successCount++;
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    failedItems.Add(source);
                    throw;
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
                    long fileSize = new FileInfo(source).Length;
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
            var sourceInfo = new DirectoryInfo(sourceDir);
            var targetInfo = new DirectoryInfo(targetDir);
            targetInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
            targetInfo.LastAccessTimeUtc = sourceInfo.LastAccessTimeUtc;
            targetInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;

            // Get all files and subdirectories
            var files = Directory.GetFiles(sourceDir);
            var subDirs = Directory.GetDirectories(sourceDir);
            int totalItems = files.Length + subDirs.Length;
            int processedItems = 0;

            // Copy files
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);
                long fileSize = new FileInfo(file).Length;

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
            // Create target directory
            Directory.CreateDirectory(targetDir);

            // Get all files and subdirectories
            var files = Directory.GetFiles(sourceDir);
            var subDirs = Directory.GetDirectories(sourceDir);
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
            bool isDirectory = Directory.Exists(path);
            if (isDirectory)
            {
                await DeleteDirectoryAsync(path, totalCount, processedCount, reportProgress, cancellationToken);
            }
            else
            {
                int basePercentage = processedCount * 100 / totalCount;
                reportProgress(basePercentage, path);
                _strategy.DeleteFile(path);
            }
        }

        private async Task DeleteDirectoryAsync(string dirPath, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
        {
            // Get all files and subdirectories
            var files = Directory.GetFiles(dirPath);
            var subDirs = Directory.GetDirectories(dirPath);
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
    }
}