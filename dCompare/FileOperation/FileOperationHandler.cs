using CommunityToolkit.Mvvm.Messaging;
using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.FileOperation.Messages;
using dCompare.FileOperation.Models;
using System.IO;
using System.Text;
using System.Windows;

namespace dCompare.FileOperation
{
    public class FileOperationHandler
    {
        private readonly IFileManager _fileManager;
        private readonly IMessenger _messenger;

        // Dictionary to track active jobs and their completion sources.
        private readonly Dictionary<string, TaskCompletionSource<bool>> _activeJobs = new();

        public FileOperationHandler(IFileManager fileManager, IMessenger messenger)
        {
            _fileManager = fileManager;
            _messenger = messenger;
        }

        public List<(string Source, string TargetPath, SourceType Type)> GetItemsForTransfer(List<TreeNode> selectedItems, bool toLeft, string targetRoot)
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

                    _messenger.Send(new QueueTransferMessage(transfer));

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var progress = new Progress<OperationProgress>(p =>
                            {
                                transfer.Progress = p.Percentage / 100.0;
                                transfer.Status = $"Deleting ({p.Percentage:P0})";
                            });

                            OperationResult result = await _fileManager.DeleteAsync(item.Path, progress);
                            if (result.SuccessCount > 0)
                            {
                                _messenger.Send(new CompleteTransferMessage(transfer, true, "Operation completed successfully."));
                                tcs.TrySetResult(true);
                            }
                            else
                            {
                                throw new Exception(string.Join("; ", result.FailedItems));
                            }
                        }
                        catch (Exception ex)
                        {
                            _messenger.Send(new CompleteTransferMessage(transfer, false, ex.Message));
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

                    _messenger.Send(new QueueTransferMessage(transfer));

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var progress = new Progress<OperationProgress>(p =>
                            {
                                transfer.Progress = p.Percentage / 100.0;
                                transfer.Status = $"Transferring";
                                if (p.Percentage > 0)
                                {
                                    transfer.TransferSpeed = (transfer.FileSize * (p.Percentage / 100.0) / 1024.0) / (DateTime.Now - transfer.StartTime).TotalSeconds;
                                }
                            });

                            OperationResult result = operation == "Copy"
                                ? await _fileManager.CopyAsync(item.Source, item.TargetPath, true, progress)
                                : await _fileManager.MoveAsync(item.Source, item.TargetPath, true, progress);

                            if (result.SuccessCount > 0)
                            {
                                _messenger.Send(new CompleteTransferMessage(transfer, true, "Operation completed successfully."));
                                tcs.TrySetResult(true);
                            }
                            else
                            {
                                throw new Exception(string.Join("; ", result.FailedItems));
                            }
                        }
                        catch (Exception ex)
                        {
                            _messenger.Send(new CompleteTransferMessage(transfer, false, ex.Message));
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

        public async Task CopyToLeft(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, destDir);
            await ExecuteTransferAsync(items, "Copy", "Error copying items");
        }

        public async Task CopyToRight(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, destDir);
            await ExecuteTransferAsync(items, "Copy", "Error copying items");
        }

        public async Task MoveToLeft(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, destDir);
            await ExecuteTransferAsync(items, "Move", "Error moving items");
        }

        public async Task MoveToRight(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, destDir);
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

        public static void ShowPropertiesDialog(TreeNode node)
        {
            StringBuilder properties = new();
            properties.AppendLine($"名稱: {node.Name}");
            properties.AppendLine($"狀態: {GetStatusText(node.Status)}");
            properties.AppendLine($"相對路徑: {node.RelativePath}");
            properties.AppendLine($"類型: {(node.IsDirectory ? "資料夾" : "檔案")}");

            if (node.LeftNode != null)
            {
                properties.AppendLine();
                properties.AppendLine("左側:");
                properties.AppendLine($"  完整路徑: {node.LeftNode.FullPath}");
                if (!node.LeftNode.IsDirectory)
                {
                    properties.AppendLine($"  大小: {node.LeftSize}");
                }
                properties.AppendLine($"  修改時間: {node.LeftDate}");
            }

            if (node.RightNode != null)
            {
                properties.AppendLine();
                properties.AppendLine("右側:");
                properties.AppendLine($"  完整路徑: {node.RightNode.FullPath}");
                if (!node.RightNode.IsDirectory)
                {
                    properties.AppendLine($"  大小: {node.RightSize}");
                }
                properties.AppendLine($"  修改時間: {node.RightDate}");
            }

            MessageBox.Show(properties.ToString(), "檔案屬性", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void OpenFileLocation(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(filePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", filePath);
                }
                else
                {
                    MessageBox.Show("路徑不存在");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法開啟檔案位置: {ex.Message}");
            }
        }

        private static string GetStatusText(ComparisonStatus status)
        {
            return status switch
            {
                ComparisonStatus.Identical => "相同",
                ComparisonStatus.Modified => "已修改",
                ComparisonStatus.LeftOnly => "僅存於左側",
                ComparisonStatus.RightOnly => "僅存於右側",
                ComparisonStatus.Conflict => "衝突",
                _ => "未知"
            };
        }
    }
}