using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using dCompare.Common;
using dCompare.FileOperation.Messages;
using dCompare.FileOperation.Models;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;

namespace dCompare.FileOperation
{
    public partial class FileOperationControlViewModel : ObservableObject,
        IRecipient<QueueTransferMessage>,
        IRecipient<CompleteTransferMessage>
    {
        // The DI singleton CompareControl transfers through as well, which is what makes the Pause and
        // Cancel buttons below act on transfers started from the results list.
        private readonly IFileManager _fileManager;
        private bool _isPaused;

        [ObservableProperty]
        private ObservableCollection<FileTransfer> _queuedTransfers = new();

        [ObservableProperty]
        private ObservableCollection<FileTransfer> _successfulTransfers = new();

        [ObservableProperty]
        private ObservableCollection<FileTransfer> _failedTransfers = new();

        [ObservableProperty]
        private string? _filterText;

        public FileOperationControlViewModel(IFileManager? fileManager = null)
        {
            _fileManager = fileManager ?? new FileManager();

            // Register for messages
            WeakReferenceMessenger.Default.Register<QueueTransferMessage>(this);
            WeakReferenceMessenger.Default.Register<CompleteTransferMessage>(this);

            var queuedView = CollectionViewSource.GetDefaultView(QueuedTransfers);
            queuedView.Filter = FilterTransfers;
            var successfulView = CollectionViewSource.GetDefaultView(SuccessfulTransfers);
            successfulView.Filter = FilterTransfers;
            var failedView = CollectionViewSource.GetDefaultView(FailedTransfers);
            failedView.Filter = FilterTransfers;
        }

        [RelayCommand(CanExecute = nameof(CanStart))]
        private void OnStart()
        {
            _isPaused = false;
            _fileManager.Resume();
        }

        private bool CanStart() => _isPaused;

        [RelayCommand(CanExecute = nameof(CanPause))]
        private void OnPause()
        {
            _isPaused = true;
            _fileManager.Pause();
        }

        private bool CanPause() => !_isPaused;

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void OnCancel()
        {
            _fileManager.Cancel();
        }

        private bool CanCancel() => QueuedTransfers.Any();

        [RelayCommand(CanExecute = nameof(CanCancelTransfer))]
        private void OnCancelTransfer(object parameter)
        {
            // shortcut: the manager owns one stop request for all queued work, so right-clicking one row
            // cancels the whole queue. Upgrade when each transfer gets its own token.
            if (parameter is FileTransfer)
            {
                _fileManager.Cancel();
            }
        }

        private bool CanCancelTransfer(object parameter) => parameter is FileTransfer;

        [RelayCommand(CanExecute = nameof(CanRetryTransfer))]
        private void OnRetryTransfer(object parameter)
        {
            if (parameter is FileTransfer transfer)
            {
                FailedTransfers.Remove(transfer);
                Transfer(transfer.Source, transfer.Target, true, transfer.Operation == FileOperationTypes.Copy, transfer.SourceType);
            }
        }

        private bool CanRetryTransfer(object parameter) => parameter is FileTransfer;

        [RelayCommand]
        private void OnFilterTextChanged()
        {
            CollectionViewSource.GetDefaultView(QueuedTransfers).Refresh();
            CollectionViewSource.GetDefaultView(SuccessfulTransfers).Refresh();
            CollectionViewSource.GetDefaultView(FailedTransfers).Refresh();
        }

        [RelayCommand(CanExecute = nameof(CanClearQueued))]
        private void OnClearQueued()
        {
            QueuedTransfers.Clear();
        }

        private bool CanClearQueued() => QueuedTransfers.Any();

        [RelayCommand(CanExecute = nameof(CanClearSuccessful))]
        private void OnClearSuccessful()
        {
            SuccessfulTransfers.Clear();
        }

        private bool CanClearSuccessful() => SuccessfulTransfers.Any();

        [RelayCommand(CanExecute = nameof(CanClearFailed))]
        private void OnClearFailed()
        {
            FailedTransfers.Clear();
        }

        private bool CanClearFailed() => FailedTransfers.Any();

        public void ClearQueuedTransfers()
        {
            App.Current.Dispatcher.Invoke(() => QueuedTransfers.Clear());
        }

        public void ClearSuccessfulTransfers()
        {
            App.Current.Dispatcher.Invoke(() => SuccessfulTransfers.Clear());
        }

        public void ClearFailedTransfers()
        {
            App.Current.Dispatcher.Invoke(() => FailedTransfers.Clear());
        }

        public void Receive(QueueTransferMessage message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                QueuedTransfers.Add(message.Transfer);
            });
        }

        public void Receive(CompleteTransferMessage message)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var transfer = message.Transfer;
                QueuedTransfers.Remove(transfer);
                transfer.Status = message.Success
                    ? FileOperationStatus.Completed
                    : message.Cancelled ? FileOperationStatus.Cancelled : FileOperationStatus.Failed;
                transfer.Progress = message.Success ? 1.0 : transfer.Progress;
                if (message.Success)
                {
                    SuccessfulTransfers.Add(transfer);
                }
                else
                {
                    FailedTransfers.Add(transfer);
                }
                LogTransfer(transfer);
            });
        }

        private static long GetItemSize(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }
                return 0; // Return 0 for directories
            }
            catch
            {
                return 0;
            }
        }

        public string Transfer(string source, string target, bool overwrite, bool isCopy, SourceType sourceType)
        {
            Log.Information($"Start to cp/mv {source} to {target}");
            string jobId = Guid.NewGuid().ToString();
            var transfer = new FileTransfer
            {
                JobId = jobId,
                Operation = isCopy ? FileOperationTypes.Copy : FileOperationTypes.Move,
                Source = source,
                Target = target,
                Status = FileOperationStatus.Queued,
                FileSize = GetItemSize(source),
                SourceType = sourceType
            };

            App.Current.Dispatcher.Invoke(() => QueuedTransfers.Add(transfer));

            Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<OperationProgress>(p =>
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            transfer.Progress = p.Percentage / 100.0;
                            transfer.Status = FileOperationStatus.Transferring;
                            if (p.Percentage > 0)
                            {
                                transfer.TransferSpeed = (transfer.FileSize * (p.Percentage / 100.0) / 1024.0) / (DateTime.Now - transfer.StartTime).TotalSeconds;
                            }
                        });
                    });

                    transfer.StartTime = DateTime.Now;

                    OperationResult result;
                    if (isCopy)
                    {
                        result = await _fileManager.CopyAsync(source, target, overwrite, progress);
                    }
                    else
                    {
                        result = await _fileManager.MoveAsync(source, target, overwrite, progress);
                    }

                    if (result.SuccessCount > 0)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            QueuedTransfers.Remove(transfer);
                            transfer.Status = FileOperationStatus.Completed;
                            transfer.Progress = 1.0;
                            SuccessfulTransfers.Add(transfer);
                            LogTransfer(transfer);
                        });

                    }
                    else
                    {
                        throw new Exception(string.Join("; ", result.FailedItems));
                    }
                }
                catch (OperationCanceledException)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        QueuedTransfers.Remove(transfer);
                        transfer.Status = FileOperationStatus.Cancelled;
                        FailedTransfers.Add(transfer);
                        LogTransfer(transfer);
                    });
                }
                catch (Exception)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        QueuedTransfers.Remove(transfer);
                        transfer.Status = FileOperationStatus.Failed;
                        FailedTransfers.Add(transfer);
                        LogTransfer(transfer);
                    });
                }
            });
            return jobId;
        }

        // A plain transfer ledger in Documents, deliberately beside (not through) the Serilog file sink so it
        // stays readable after a restart. It must never fail the transfer it describes: this runs inside the
        // messenger call on the worker thread, so an exception here (OneDrive-redirected Documents, an AV
        // lock) would unwind into the worker's catch and book the same transfer as failed as well.
        private static void LogTransfer(FileTransfer transfer)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileOperationLog.txt");
                string logEntry = $"{DateTime.Now}: {transfer.Operation} - {transfer.Source} to {transfer.Target} - {transfer.Status.ToString()}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not append the transfer record for {Source} to FileOperationLog.txt.", transfer.Source);
            }
        }

        private bool FilterTransfers(object item)
        {
            if (string.IsNullOrEmpty(FilterText))
                return true;

            var transfer = item as FileTransfer;
            string filter = FilterText.ToLowerInvariant();
            return transfer?.Source?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
                   transfer?.Target?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
