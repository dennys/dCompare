using dCompare.Compare.Models;
using dCompare.FileOperation;
using dCompare.FileOperation.Models;
using Serilog;
using System.IO;
using System.Windows;

namespace dCompare.Compare
{
    public class FileOperationHandler
    {
        private readonly IFileManager _fileManager;
        private readonly FileOperationControl _fileOperationControl;

        // Dictionary to track active jobs and their completion sources.
        private readonly Dictionary<string, TaskCompletionSource<bool>> _activeJobs = new();

        public FileOperationHandler(IFileManager fileManager)
        {
            _fileManager = fileManager;
            _fileOperationControl = FindFileOperationControl();
        }

        // Helper method to find FileOperationControl in the visual tree
        private FileOperationControl FindFileOperationControl()
        {
            var mainWindow = Application.Current.MainWindow;

            // Find FileOperationControl by x:Name or type
            FileOperationControl? control = (FileOperationControl)mainWindow.FindName("fileOperationControl");
            // Fallback: Traverse the visual tree to find FileOperationControl
            control ??= WpfTreeHelperExtensions.FindVisualChild<FileOperationControl>(mainWindow);

            if (control is null)
            {
                const string message = "FileOperationControl not found in visual tree.";
                Log.Error(message);
                throw new InvalidOperationException(message);
            }

            control.OnTransferStatusChanged += FileOperation_OnTransferStatusChanged;
            return control;
        }

        public List<(string Source, string TargetPath, SourceType Type)> GetItemsForTransfer(List<TreeNode> selectedItems, bool toLeft, string targetRoot, string sourceRoot)
        {
            List<(string Source, string TargetPath, SourceType Type)> items = new();
            foreach (var node in selectedItems)
            {
                string? sourcePath = toLeft ? node.RightNode?.FullPath : node.LeftNode?.FullPath;
                if (string.IsNullOrEmpty(sourcePath)) continue;

                string relativePath = node.RelativePath ?? string.Empty;
                string destDir = Path.Combine(targetRoot, Path.GetDirectoryName(relativePath) ?? string.Empty);
                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(destDir, fileName);

                items.Add((sourcePath, destPath, toLeft ? SourceType.Right : SourceType.Left));
            }
            return items;
        }

        public List<(string Path, SourceType Type)> GetItemsForDeletion(List<TreeNode> selectedItems, bool isLeft)
        {
            List<(string Path, SourceType Type)> paths = new();
            foreach (var node in selectedItems)
            {
                string? path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add((path, isLeft ? SourceType.Left : SourceType.Right));
                }
            }
            return paths;
        }

        public async Task DeleteAsync(List<(string Path, SourceType Type)> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            try
            {
                if (_fileOperationControl == null)
                {
                    throw new Exception("FileOperationControl is not available.");
                }

                var jobIds = new List<string>();
                var tasks = new List<Task>();

                foreach (var item in items)
                {
                    string jobId = Guid.NewGuid().ToString();
                    jobIds.Add(jobId);
                    var tcs = new TaskCompletionSource<bool>();
                    _activeJobs[jobId] = tcs;

                    var transfer = new FileTransfer
                    {
                        JobId = jobId,
                        Operation = "Delete",
                        Source = item.Path,
                        Target = string.Empty,
                        Status = "Queued",
                        FileSize = 0,
                        SourceType = item.Type,
                        StartTime = DateTime.Now
                    };

                    Application.Current.Dispatcher.Invoke(() => _fileOperationControl.QueuedTransfers.Add(transfer));

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var progress = new Progress<OperationProgress>(p =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    transfer.Progress = p.Percentage / 100.0;
                                    transfer.Status = $"Deleting ({p.Percentage:P0})";
                                });
                            });

                            OperationResult result = await _fileManager.DeleteAsync(item.Path, progress);
                            if (result.SuccessCount > 0)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _fileOperationControl.CompleteTransfer(transfer, true, "Operation completed successfully.");
                                });
                                tcs.TrySetResult(true);
                            }
                            else
                            {
                                throw new Exception(string.Join("; ", result.FailedItems));
                            }
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _fileOperationControl.CompleteTransfer(transfer, false, ex.Message);
                            });
                            tcs.TrySetException(ex);
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deleting items: {ex.Message}");
            }
        }

        // Common method to handle copy or move operations
        private async Task ExecuteTransferAsync(List<(string Source, string TargetPath, SourceType Type)> items, string operation, string errorMessage)
        {
            if (items.Count == 0)
            {
                MessageBox.Show($"請選擇至少一個文件或目錄進行{operation.ToLower()}");
                return;
            }

            try
            {
                if (_fileOperationControl == null)
                {
                    throw new Exception("FileOperationControl is not available.");
                }

                var jobIds = new List<string>();
                var tasks = new List<Task>();

                foreach (var item in items)
                {
                    string jobId = Guid.NewGuid().ToString();
                    jobIds.Add(jobId);
                    var tcs = new TaskCompletionSource<bool>();
                    _activeJobs[jobId] = tcs;

                    var transfer = new FileTransfer
                    {
                        JobId = jobId,
                        Operation = operation,
                        Source = item.Source,
                        Target = item.TargetPath,
                        Status = "Queued",
                        FileSize = Directory.Exists(item.Source) ? 0 : new FileInfo(item.Source).Length,
                        SourceType = item.Type,
                        StartTime = DateTime.Now
                    };

                    Application.Current.Dispatcher.Invoke(() => _fileOperationControl.QueuedTransfers.Add(transfer));

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var progress = new Progress<OperationProgress>(p =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    transfer.Progress = p.Percentage / 100.0;
                                    transfer.Status = $"Transferring";
                                    if (p.Percentage > 0)
                                    {
                                        transfer.TransferSpeed = (transfer.FileSize * (p.Percentage / 100.0) / 1024.0) / (DateTime.Now - transfer.StartTime).TotalSeconds;
                                    }
                                });
                            });

                            OperationResult result = operation == "Copy"
                                ? await _fileManager.CopyAsync(item.Source, item.TargetPath, true, progress)
                                : await _fileManager.MoveAsync(item.Source, item.TargetPath, true, progress);

                            if (result.SuccessCount > 0)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    _fileOperationControl.CompleteTransfer(transfer, true, "Operation completed successfully.");
                                });
                                tcs.TrySetResult(true);
                            }
                            else
                            {
                                throw new Exception(string.Join("; ", result.FailedItems));
                            }
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _fileOperationControl.CompleteTransfer(transfer, false, ex.Message);
                            });
                            tcs.TrySetException(ex);
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{errorMessage}: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task CopyToLeft(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, leftRoot, rightRoot);
            await ExecuteTransferAsync(items, "Copy", "Error copying items");
        }

        public async Task CopyToRight(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, rightRoot, leftRoot);
            await ExecuteTransferAsync(items, "Copy", "Error copying items");
        }

        public async Task MoveToLeft(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, leftRoot, rightRoot);
            await ExecuteTransferAsync(items, "Move", "Error moving items");
        }

        public async Task MoveToRight(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, rightRoot, leftRoot);
            await ExecuteTransferAsync(items, "Move", "Error moving items");
        }

        /// <summary>
        /// Renames a file or directory on the file system. This method is testable.
        /// </summary>
        /// <param name="sourcePath">The full path of the item to rename.</param>
        /// <param name="newName">The new name for the item (name and extension only).</param>
        /// <returns>The new full path of the renamed item.</returns>
        public string RenameFileOrDirectory(string sourcePath, string newName)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path cannot be null or empty.", nameof(sourcePath));

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("New name cannot be null or empty.", nameof(newName));

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("New name contains invalid characters.", nameof(newName));

            string? parentDir = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(parentDir))
            {
                throw new InvalidOperationException($"Cannot determine parent directory for '{sourcePath}'.");
            }

            string newPath = Path.Combine(parentDir, newName);

            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, newPath);
            }
            else if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, newPath);
            }
            else
            {
                throw new FileNotFoundException("The specified file or directory does not exist.", sourcePath);
            }

            return newPath;
        }

        private void FileOperation_OnTransferStatusChanged(string jobId, bool success, string message)
        {
            string a = $"Job {jobId}: {message}";
            Log.Information($"complete copy or move or delete: {a}");

            // Find the TaskCompletionSource for the completed job.
            if (_activeJobs.TryGetValue(jobId, out var tcs))
            {
                if (success)
                {
                    // Signal that the task completed successfully.
                    tcs.TrySetResult(true);
                }
                else
                {
                    // Signal that the task failed.
                    tcs.TrySetException(new Exception(message));
                }
                // Remove the completed job from tracking.
                _activeJobs.Remove(jobId);
            }
        }
    }
}