using CommunityToolkit.Mvvm.Messaging;
using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.FileOperation.Messages;
using dCompare.FileOperation.Models;
using Serilog;
using System.IO;
using System.Text;
using System.Windows;

namespace dCompare.FileOperation
{
    public class FileOperationHandler
    {
        private readonly IFileManager _fileManager;
        private readonly IMessenger _messenger;
        private readonly IConfigurationService? _configService;
        private readonly IUserInteractionService? _userInteraction;

        // The two confirmation services are optional so the handler stays constructible without the
        // container (unit tests); without them no prompt can be raised and the operation runs unconfirmed.
        public FileOperationHandler(IFileManager fileManager, IConfigurationService? configService = null, IUserInteractionService? userInteraction = null)
        {
            _fileManager = fileManager;
            _messenger = CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default;
            _configService = configService;
            _userInteraction = userInteraction;
        }

        // One CTS covers a whole batch, so the operation panel's Cancel button stops a multi-GB transfer
        // started from the results list. Concurrent batches share the live source; it is replaced, not
        // reset, once cancelled, because CancellationTokenSource cannot be un-cancelled.
        private readonly object _batchGate = new();
        private CancellationTokenSource? _batchCts;
        private int _activeBatches;

        // A selection can hold thousands of rows and every one of them used to start its own task, so a large
        // batch opened thousands of file handles at once and starved the thread pool. The rows still all queue
        // up front for the transfer panel; only the actual I/O is throttled.
        private readonly SemaphoreSlim _transferGate = new(4, 4);

        public void Cancel()
        {
            lock (_batchGate)
            {
                _batchCts?.Cancel();
            }
        }

        private CancellationToken StartBatch()
        {
            lock (_batchGate)
            {
                _activeBatches++;
                if (_batchCts == null || _batchCts.IsCancellationRequested)
                {
                    // The cancelled instance is left undisposed: a batch still winding down may hold a
                    // registration on it, and it has already delivered its cancellation.
                    _batchCts = new CancellationTokenSource();
                }
                return _batchCts.Token;
            }
        }

        private void EndBatch()
        {
            lock (_batchGate)
            {
                if (--_activeBatches == 0 && _batchCts != null)
                {
                    _batchCts.Dispose();
                    _batchCts = null;
                }
            }
        }

        public List<(string Source, string TargetPath, SourceType Type)> GetItemsForTransfer(List<TreeNode> selectedItems, bool toLeft, string targetRoot)
        {
            List<(string Source, string TargetPath, SourceType Type)> items = new();

            // An empty or relative root turns Path.Combine into a path under the process working directory,
            // which silently writes the user's files into the folder the exe happened to be launched from.
            if (string.IsNullOrWhiteSpace(targetRoot) || !Path.IsPathRooted(targetRoot))
            {
                throw new ArgumentException($"'{targetRoot}' is not a valid destination folder.", nameof(targetRoot));
            }

            foreach (var node in selectedItems)
            {
                if (node.IsArchiveEntry) continue;

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
                if (node.IsArchiveEntry) continue;

                string? path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add((path, isLeft ? SourceType.Left : SourceType.Right));
                }
            }
            return paths;
        }

        public static bool ContainsArchiveEntry(IEnumerable<TreeNode> selectedItems) => selectedItems.Any(node => node.IsArchiveEntry);

        public async Task DeleteAsync(List<(string Path, SourceType Type)> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            // Deleting is the one operation the app cannot undo from its own UI, so the user approves the
            // exact targets here, on the UI thread, before any worker thread is started.
            if (_configService != null && _userInteraction != null)
            {
                AppConfiguration config = await _configService.LoadConfigurationAsync();
                // Same null fallback as the copy/move gate: DeleteFile defaults to true, so a config without an
                // Options section must still prompt rather than delete silently.
                bool confirmDelete = (config.Options?.Confirmation ?? new ConfirmationOptions()).DeleteFile;
                if (confirmDelete && !_userInteraction.ShowConfirmation(BuildDeleteMessage(items), Properties.Resources.ConfirmOperation))
                {
                    Log.Information("Delete of {Count} item(s) cancelled by the user.", items.Count);
                    _userInteraction.ShowInformation(
                        string.Format(Properties.Resources.StatusDeleteCancelled, items.Count),
                        Properties.Resources.ConfirmOperation);
                    return;
                }
            }

            // The items themselves are deleted on worker threads below.
            CancellationToken token = StartBatch();
            try
            {
                var tasks = new List<Task>();

                foreach (var item in items)
                {
                    string jobId = Guid.NewGuid().ToString();
                    // One token per row (linked to the batch token), so the panel's per-row Cancel stops
                    // exactly this transfer while the rest of the batch keeps running.
                    var itemCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                    var transfer = new FileTransfer
                    {
                        JobId = jobId,
                        Operation = FileOperationTypes.Delete,
                        Source = item.Path,
                        Target = string.Empty,
                        Status = FileOperationStatus.Queued,
                        FileSize = 0,
                        SourceType = item.Type,
                        StartTime = DateTime.Now,
                        Cancellation = itemCts
                    };

                    _messenger.Send(new QueueTransferMessage(transfer));

                    tasks.Add(Task.Run(async () =>
                    {
                        bool holdingSlot = false;
                        try
                        {
                            await _transferGate.WaitAsync(itemCts.Token);
                            holdingSlot = true;

                            var progress = new Progress<OperationProgress>(p =>
                            {
                                transfer.Progress = p.Percentage / 100.0;
                                transfer.Status = FileOperationStatus.Transferring;
                            });

                            OperationResult result = await _fileManager.DeleteAsync(item.Path, progress, itemCts.Token);
                            if (result.SuccessCount > 0)
                            {
                                _messenger.Send(new CompleteTransferMessage(transfer, true, Properties.Resources.OperationCompletedSuccessfully));
                            }
                            else
                            {
                                throw new Exception(string.Join("; ", result.FailedItems));
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // A cancelled item is a reported outcome, not a batch error: no error dialog afterwards.
                            _messenger.Send(new CompleteTransferMessage(transfer, false, Properties.Resources.TransferCanceled, cancelled: true));
                        }
                        catch (Exception ex)
                        {
                            _messenger.Send(new CompleteTransferMessage(transfer, false, ex.Message));
                            throw;
                        }
                        finally
                        {
                            if (holdingSlot) _transferGate.Release();
                            itemCts.Dispose();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(Properties.Resources.ErrorDeletingItems, ex.Message));
            }
            finally
            {
                EndBatch();
            }
        }

        // Common method to handle copy or move operations
        private async Task ExecuteTransferAsync(List<(string Source, string TargetPath, SourceType Type)> items, FileOperationTypes operation, string targetRoot, string errorMessage)
        {
            if (items.Count == 0)
            {
                MessageBox.Show(string.Format(Properties.Resources.PleaseSelectFileOrDirectory, operation.ToString().ToLower()));
                return;
            }

            // Everything below this line runs on worker threads, so this is the only place a prompt is legal.
            bool overwrites = items.Any(item => File.Exists(item.TargetPath) || Directory.Exists(item.TargetPath));
            if (!await ConfirmTransferAsync(items, operation, targetRoot, overwrites))
            {
                string cancelledVerb = operation == FileOperationTypes.Copy
                    ? Properties.Resources.VerbCopy
                    : Properties.Resources.VerbMove;
                Log.Information("{Operation} of {Count} item(s) to {Target} cancelled by the user.", operation, items.Count, targetRoot);
                _userInteraction?.ShowInformation(
                    string.Format(Properties.Resources.StatusOperationCancelled, cancelledVerb, items.Count, targetRoot),
                    Properties.Resources.ConfirmOperation);
                return;
            }

            CancellationToken token = StartBatch();
            try
            {
                var tasks = new List<Task>();

                foreach (var item in items)
                {
                    string jobId = Guid.NewGuid().ToString();
                    // One token per row (linked to the batch token), so the panel's per-row Cancel stops
                    // exactly this transfer while the rest of the batch keeps running.
                    var itemCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                    var transfer = new FileTransfer
                    {
                        JobId = jobId,
                        Operation = operation,
                        Source = item.Source,
                        Target = item.TargetPath,
                        Status = FileOperationStatus.Queued,
                        FileSize = Directory.Exists(item.Source) ? 0 : new FileInfo(item.Source).Length,
                        SourceType = item.Type,
                        StartTime = DateTime.Now,
                        Cancellation = itemCts
                    };

                    _messenger.Send(new QueueTransferMessage(transfer));

                    tasks.Add(Task.Run(async () =>
                    {
                        bool holdingSlot = false;
                        try
                        {
                            await _transferGate.WaitAsync(itemCts.Token);
                            holdingSlot = true;

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
                                ? await _fileManager.CopyAsync(item.Source, item.TargetPath, overwrites, progress, itemCts.Token)
                                : await _fileManager.MoveAsync(item.Source, item.TargetPath, overwrites, progress, itemCts.Token);

                            if (result.SuccessCount > 0)
                            {
                                _messenger.Send(new CompleteTransferMessage(transfer, true, Properties.Resources.OperationCompletedSuccessfully));
                            }
                            else
                            {
                                throw new Exception(string.Join("; ", result.FailedItems));
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // A cancelled item is a reported outcome, not a batch error: no error dialog afterwards.
                            _messenger.Send(new CompleteTransferMessage(transfer, false, Properties.Resources.TransferCanceled, cancelled: true));
                        }
                        catch (Exception ex)
                        {
                            _messenger.Send(new CompleteTransferMessage(transfer, false, ex.Message));
                            throw;
                        }
                        finally
                        {
                            if (holdingSlot) _transferGate.Release();
                            itemCts.Dispose();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{errorMessage}: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EndBatch();
            }
        }

        // The user is approving a destructive action, so the prompt names what actually goes: the item count
        // and the paths, truncated so the dialog stays readable for large selections.
        private static string BuildDeleteMessage(List<(string Path, SourceType Type)> items)
        {
            const int listedPaths = 5;
            string paths = string.Join(Environment.NewLine, items.Take(listedPaths).Select(item => item.Path));
            if (items.Count > listedPaths)
            {
                paths += Environment.NewLine + string.Format(Properties.Resources.ConfirmDeleteMoreItems, items.Count - listedPaths);
            }
            return string.Format(Properties.Resources.ConfirmDeleteItems, items.Count) + Environment.NewLine + paths;
        }

        // Options | Confirmation used to be write-only: the Options page stored Copy / Move / CopyOverwrite /
        // MoveOverwrite and nothing read them. Copy and Move confirm every batch, CopyOverwrite and
        // MoveOverwrite only when something at the destination would actually be replaced.
        private async Task<bool> ConfirmTransferAsync(List<(string Source, string TargetPath, SourceType Type)> items, FileOperationTypes operation, string targetRoot, bool overwrites)
        {
            if (_configService == null || _userInteraction == null)
            {
                return true;
            }

            AppConfiguration config = await _configService.LoadConfigurationAsync();
            ConfirmationOptions confirmation = config.Options?.Confirmation ?? new ConfirmationOptions();
            bool isCopy = operation == FileOperationTypes.Copy;

            if (!(isCopy ? confirmation.Copy : confirmation.Move) &&
                !(overwrites && (isCopy ? confirmation.CopyOverwrite : confirmation.MoveOverwrite)))
            {
                return true;
            }

            string verb = isCopy ? Properties.Resources.VerbCopy : Properties.Resources.VerbMove;
            string message = string.Format(Properties.Resources.ConfirmTransferMessage, verb, items.Count, targetRoot);
            if (overwrites)
            {
                message += Environment.NewLine + Properties.Resources.ConfirmTransferOverwriteWarning;
            }

            return _userInteraction.ShowConfirmation(message, Properties.Resources.ConfirmOperation);
        }

        public async Task CopyToLeft(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, destDir);
            await ExecuteTransferAsync(items, FileOperationTypes.Copy, destDir, Properties.Resources.ErrorCopyingItems);
        }

        public async Task CopyToRight(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, destDir);
            await ExecuteTransferAsync(items, FileOperationTypes.Copy, destDir, Properties.Resources.ErrorCopyingItems);
        }

        public async Task MoveToLeft(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, destDir);
            await ExecuteTransferAsync(items, FileOperationTypes.Move, destDir, Properties.Resources.ErrorMovingItems);
        }

        public async Task MoveToRight(List<TreeNode> selectedItems, string destDir)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, destDir);
            await ExecuteTransferAsync(items, FileOperationTypes.Move, destDir, Properties.Resources.ErrorMovingItems);
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