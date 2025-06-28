using dCompare.FileOperation;
using dCompare.FileOperation.Models;
using Microsoft.VisualBasic;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace dCompare.Compare
{
    public class FileOperationHandler
    {
        private readonly IFileManager _fileManager;
        private readonly TreeNodeManager _treeNodeManager;
        private FileOperationControl _fileOperationControl;
        private FileOperationWindow _fileOperationWindow;

        // Dictionary to track active jobs and their completion sources.
        private readonly Dictionary<string, TaskCompletionSource<bool>> _activeJobs = new();

        public FileOperationHandler(IFileManager fileManager, TreeNodeManager treeNodeManager)
        {
            _fileManager = fileManager;
            _treeNodeManager = treeNodeManager;
            FindFileOperationControl();
        }

        // Helper method to find FileOperationControl in the visual tree
        private FileOperationControl FindFileOperationControl()
        {
            if (_fileOperationControl != null)
            {
                return _fileOperationControl; // Return cached instance
            }

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                Log.Warning("MainWindow is not available.");
                return null;
            }

            // Find FileOperationControl by x:Name or type
            _fileOperationControl = mainWindow.FindName("fileOperationControl") as FileOperationControl;
            if (_fileOperationControl == null)
            {
                // Fallback: Traverse the visual tree to find FileOperationControl
                _fileOperationControl = WpfTreeHelperExtensions.FindVisualChild<FileOperationControl>(mainWindow);
            }

            if (_fileOperationControl != null)
            {
                // Subscribe to the event if not already subscribed
                _fileOperationControl.OnTransferStatusChanged += FileOperation_OnTransferStatusChanged;
            }
            else
            {
                Log.Error("Could not find FileOperationControl in the visual tree.");
            }

            return _fileOperationControl;
        }

        public List<(string Source, string TargetPath, SourceType Type)> GetItemsForTransfer(List<TreeNode> selectedItems, bool toLeft, string targetRoot, string sourceRoot)
        {
            List<(string Source, string TargetPath, SourceType Type)> items = new();
            foreach (var node in selectedItems)
            {
                string sourcePath = toLeft ? node.RightNode?.FullPath : node.LeftNode?.FullPath;
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
                string path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
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
                                    transfer.Status = $"Transferring ({p.Percentage:P0})";
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

        public void Rename(TreeNode selectedItem, bool isLeft, string leftRoot, string rightRoot)
        {
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }

            string sourcePath = isLeft ? selectedItem.LeftNode?.FullPath : selectedItem.RightNode?.FullPath;
            if (string.IsNullOrEmpty(sourcePath))
            {
                MessageBox.Show("No valid file or directory path selected.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string currentName = Path.GetFileName(sourcePath);
            string newName = Interaction.InputBox("Enter new name:", "Rename File/Directory", currentName);

            if (string.IsNullOrEmpty(newName) || newName == currentName)
            {
                return;
            }

            try
            {
                string parentDir = Path.GetDirectoryName(sourcePath);
                string newPath = Path.Combine(parentDir, newName);

                if (selectedItem.IsDirectory)
                {
                    Directory.Move(sourcePath, newPath);
                }
                else
                {
                    File.Move(sourcePath, newPath);
                }

                string relativePath = newPath.Substring(isLeft ? leftRoot.Length : rightRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                selectedItem.RelativePath = relativePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
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