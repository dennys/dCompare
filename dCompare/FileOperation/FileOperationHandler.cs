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

        public FileOperationHandler(IFileManager fileManager)
        {
            _fileManager = fileManager;
            _messenger = CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default;
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
                        Operation = FileOperationTypes.Delete,
                        Source = item.Path,
                        Target = string.Empty,
                        Status = FileOperationStatus.Queued,
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
                                transfer.Status = FileOperationStatus.Transferring;
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
                throw new Exception(string.Format(Properties.Resources.ErrorDeletingItems, ex.Message));
            }
        }

        // Common method to handle copy or move operations
        private async Task ExecuteTransferAsync(List<(string Source, string TargetPath, SourceType Type)> items, FileOperationTypes operation, string errorMessage)
        {
            if (items.Count == 0)
            {
                MessageBox.Show(string.Format(Properties.Resources.PleaseSelectFileOrDirectory, operation.ToString().ToLower()));
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
                        Status = FileOperationStatus.Queued,
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
                                transfer.Status = FileOperationStatus.Transferring;
                                if (p.Percentage > 0)
                                {
                                    transfer.TransferSpeed = (transfer.FileSize * (p.Percentage / 100.0) / 1024.0) / (DateTime.Now - transfer.StartTime).TotalSeconds;
                                }
                            });

                            OperationResult result = operation == FileOperationTypes.Copy
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
            await ExecuteTransferAsync(items, FileOperationTypes.Copy, "Error copying items");
        }

        public async Task CopyToRight(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, destDir);
            await ExecuteTransferAsync(items, FileOperationTypes.Copy, "Error copying items");
        }

        public async Task MoveToLeft(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, destDir);
            await ExecuteTransferAsync(items, FileOperationTypes.Move, "Error moving items");
        }

        public async Task MoveToRight(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, destDir);
            await ExecuteTransferAsync(items, FileOperationTypes.Move, "Error moving items");
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
                throw new ArgumentException(Properties.Resources.ErrorInvalidSourcePath, nameof(sourcePath));

            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException(Properties.Resources.ErrorInvalidNewName, nameof(newName));

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(Properties.Resources.ErrorInvalidCharactersInNewName, nameof(newName));

            string? parentDir = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(parentDir))
            {
                throw new InvalidOperationException(string.Format(Properties.Resources.ErrorCannotDetermineParentDirectory, sourcePath));
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
                throw new FileNotFoundException(Properties.Resources.ErrorFileOrDirectoryNotFound, sourcePath);
            }

            return newPath;
        }

        public static void ShowPropertiesDialog(TreeNode node)
        {
            StringBuilder properties = new();
            properties.AppendLine($"{Properties.Resources.PropertyName}: {node.Name}");
            properties.AppendLine($"{Properties.Resources.PropertyStatus}: {GetStatusText(node.Status)}");
            properties.AppendLine($"{Properties.Resources.PropertyRelativePath}: {node.RelativePath}");
            properties.AppendLine($"{Properties.Resources.PropertyType}: {(node.IsDirectory ? Properties.Resources.PropertyFolder : Properties.Resources.PropertyFile)}");

            if (node.LeftNode != null)
            {
                properties.AppendLine();
                properties.AppendLine(Properties.Resources.PropertyLeft + ":");
                properties.AppendLine($"  {Properties.Resources.PropertyFullPath}: {node.LeftNode.FullPath}");
                if (!node.LeftNode.IsDirectory)
                {
                    properties.AppendLine($"  {Properties.Resources.PropertySize}: {node.LeftSize}");
                }
                properties.AppendLine($"  {Properties.Resources.PropertyModifiedTime}: {node.LeftDate}");
            }

            if (node.RightNode != null)
            {
                properties.AppendLine();
                properties.AppendLine(Properties.Resources.PropertyRight + ":");
                properties.AppendLine($"  {Properties.Resources.PropertyFullPath}: {node.RightNode.FullPath}");
                if (!node.RightNode.IsDirectory)
                {
                    properties.AppendLine($"  {Properties.Resources.PropertySize}: {node.RightSize}");
                }
                properties.AppendLine($"  {Properties.Resources.PropertyModifiedTime}: {node.RightDate}");
            }

            MessageBox.Show(properties.ToString(), Properties.Resources.FileProperties, MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show(Properties.Resources.PathNotExist);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.CannotOpenFileLocation, ex.Message));
            }
        }

        private static string GetStatusText(ComparisonStatus status)
        {
            return status switch
            {
                ComparisonStatus.Identical => Properties.Resources.StatusIdentical,
                ComparisonStatus.Modified => Properties.Resources.StatusModified,
                ComparisonStatus.LeftOnly => Properties.Resources.StatusLeftOnly,
                ComparisonStatus.RightOnly => Properties.Resources.StatusRightOnly,
                ComparisonStatus.Conflict => Properties.Resources.StatusConflict,
                _ => Properties.Resources.StatusUnknown
            };
        }
    }
}