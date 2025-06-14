using dCompare.FileOperation.Strategies;
using dCompare.FileOperation.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace dCompare.FileOperation;

/// <summary>
/// Manages background file and directory operations with progress and cancellation.
/// </summary>
public class FileManager
{
    private readonly IFileOperationStrategy _strategy;

    public FileManager(IFileOperationStrategy? strategy = null)
    {
        // Default to Kernel32 strategy if none is provided.
        _strategy = strategy ?? new Kernel32FileCopyStrategy();
    }

    #region Public API Methods

    public Task<OperationResult> CopyAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = new List<(string, string)> { (source, destination) };
        return ProcessItemsAsync(items, OperationType.Copy, overwrite, progress, cancellationToken);
    }

    public Task<OperationResult> MoveAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = new List<(string, string)> { (source, destination) };
        return ProcessItemsAsync(items, OperationType.Move, overwrite, progress, cancellationToken);
    }

    public Task<OperationResult> DeleteAsync(string path, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = new List<(string, string)> { (path, string.Empty) };
        return ProcessItemsAsync(items, OperationType.Delete, false, progress, cancellationToken);
    }

    public Task<OperationResult> CopyMultipleAsync(List<(string source, string target)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return ProcessItemsAsync(items, OperationType.Copy, overwrite, progress, cancellationToken);
    }

    public Task<OperationResult> MoveMultipleAsync(List<(string source, string target)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return ProcessItemsAsync(items, OperationType.Move, overwrite, progress, cancellationToken);
    }

    public Task<OperationResult> DeleteMultipleAsync(List<string> paths, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = paths.ConvertAll(p => (p, string.Empty));
        return ProcessItemsAsync(items, OperationType.Delete, false, progress, cancellationToken);
    }

    #endregion

    private enum OperationType { Copy, Move, Delete }

    private async Task<OperationResult> ProcessItemsAsync(List<(string source, string target)> items, OperationType opType, bool overwrite, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
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

        foreach (var (source, target) in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentItem = source;

            try
            {
                switch (opType)
                {
                    case OperationType.Copy:
                        await HandleCopyAsync(source, target, overwrite, totalCount, processedCount, ReportProgress, cancellationToken);
                        break;
                    case OperationType.Move:
                        HandleMove(source, target, overwrite);
                        break;
                    case OperationType.Delete:
                        HandleDelete(source);
                        break;
                }
                successCount++;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                // If cancellation caused the exception, re-throw it.
                failedItems.Add(source);
                throw;
            }
            catch (Exception)
            {
                failedItems.Add(source);
            }
            finally
            {
                processedCount++;
                if (totalCount > 1) // Only report count-based progress for multiple items
                {
                    ReportProgress(processedCount * 100 / totalCount, currentItem);
                }
            }
        }

        stopwatch.Stop();
        ReportProgress(100, string.Empty); // Final report
        return new OperationResult(successCount, failedItems.Count, failedItems, stopwatch.Elapsed);
    }

    private async Task HandleCopyAsync(string source, string target, bool overwrite, int totalCount, int processedCount, Action<int, string> reportProgress, CancellationToken cancellationToken)
    {
        bool isDirectory = Directory.Exists(source);
        if (isDirectory)
        {
            // Create destination directory and preserve metadata
            Directory.CreateDirectory(target);
            var sourceInfo = new DirectoryInfo(source);
            var targetInfo = new DirectoryInfo(target);
            targetInfo.CreationTimeUtc = sourceInfo.CreationTimeUtc;
            targetInfo.LastAccessTimeUtc = sourceInfo.LastAccessTimeUtc;
            targetInfo.LastWriteTimeUtc = sourceInfo.LastWriteTimeUtc;
        }
        else
        {
            // For single large file, use byte-level progress
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
            else // For multiple files, use item-count progress
            {
                int basePercentage = processedCount * 100 / totalCount;
                reportProgress(basePercentage, source);
                await _strategy.CopyFileAsync(source, target, overwrite, null, cancellationToken);
            }
        }
    }

    private void HandleMove(string source, string target, bool overwrite)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, target);
        }
        else
        {
            _strategy.MoveFile(source, target, overwrite);
        }
    }

    private void HandleDelete(string path)
    {
        if (Directory.Exists(path))
        {
            _strategy.DeleteDirectory(path);
        }
        else
        {
            _strategy.DeleteFile(path);
        }
    }
}