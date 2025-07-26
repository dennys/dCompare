using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Common;
using dCompare.FileOperation.Models;
using dCompare.FileOperation.Strategies;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;

namespace dCompare.FileOperation
{
    public partial class FileOperationControlViewModel : ObservableObject
    {
        private readonly FileManager _fileManager;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isPaused;

        [ObservableProperty]
        private ObservableCollection<FileTransfer> _queuedTransfers = new();

        [ObservableProperty]
        private ObservableCollection<FileTransfer> _successfulTransfers = new();

        [ObservableProperty]
        private ObservableCollection<FileTransfer> _failedTransfers = new();

        [ObservableProperty]
        private string _filterText;

        public delegate void TransferStatusChangedHandler(string jobId, bool success, string message);
        public event TransferStatusChangedHandler? OnTransferStatusChanged;

        public FileOperationControlViewModel()
        {
            _fileManager = new FileManager(new Kernel32FileCopyStrategy());
            _cancellationTokenSource = new CancellationTokenSource();

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
        }

        private bool CanStart() => _isPaused;

        [RelayCommand(CanExecute = nameof(CanPause))]
        private void OnPause()
        {
            _isPaused = true;
        }

        private bool CanPause() => !_isPaused;

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void OnCancel()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private bool CanCancel() => QueuedTransfers.Any();

        [RelayCommand(CanExecute = nameof(CanCancelTransfer))]
        private void OnCancelTransfer(object parameter)
        {
            if (parameter is FileTransfer)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private bool CanCancelTransfer(object parameter) => parameter is FileTransfer;

        [RelayCommand(CanExecute = nameof(CanRetryTransfer))]
        private void OnRetryTransfer(object parameter)
        {
            if (parameter is FileTransfer transfer)
            {
                FailedTransfers.Remove(transfer);
                Transfer(transfer.Source, transfer.Target, true, transfer.Operation == "Copy", transfer.SourceType);
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

        public void CompleteTransfer(FileTransfer transfer, bool success, string message)
        {
            QueuedTransfers.Remove(transfer);
            transfer.Status = success ? "Completed" : $"Failed: {message}";
            transfer.Progress = success ? 1.0 : transfer.Progress;
            if (success)
            {
                SuccessfulTransfers.Add(transfer);
            }
            else
            {
                FailedTransfers.Add(transfer);
            }
            LogTransfer(transfer);
            OnTransferStatusChanged?.Invoke(transfer.JobId, success, message);
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
                Operation = isCopy ? "Copy" : "Move",
                Source = source,
                Target = target,
                Status = "Queued",
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
                            transfer.Status = isCopy ? "Copying" : "Moving";
                            if (p.Percentage > 0)
                            {
                                transfer.TransferSpeed = (transfer.FileSize * (p.Percentage / 100.0) / 1024.0) / (DateTime.Now - transfer.StartTime).TotalSeconds;
                            }
                        });
                    });

                    while (_isPaused && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }

                    transfer.StartTime = DateTime.Now;

                    OperationResult result;
                    if (isCopy)
                    {
                        result = await _fileManager.CopyAsync(source, target, overwrite, progress, _cancellationTokenSource.Token);
                    }
                    else
                    {
                        result = await _fileManager.MoveAsync(source, target, overwrite, progress, _cancellationTokenSource.Token);
                    }

                    if (result.SuccessCount > 0)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            QueuedTransfers.Remove(transfer);
                            transfer.Status = "Completed";
                            transfer.Progress = 1.0;
                            SuccessfulTransfers.Add(transfer);
                            LogTransfer(transfer);
                        });

                        OnTransferStatusChanged?.Invoke(jobId, true, "Operation completed successfully.");
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
                        transfer.Status = "Cancelled";
                        FailedTransfers.Add(transfer);
                        LogTransfer(transfer);
                    });

                    OnTransferStatusChanged?.Invoke(jobId, false, "Operation cancelled.");
                }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        QueuedTransfers.Remove(transfer);
                        transfer.Status = $"Failed: {ex.Message}";
                        FailedTransfers.Add(transfer);
                        LogTransfer(transfer);
                    });

                    OnTransferStatusChanged?.Invoke(jobId, false, ex.Message);
                }
            }, _cancellationTokenSource.Token);
            return jobId;
        }

        private static void LogTransfer(FileTransfer transfer)
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileOperationLog.txt");
            string logEntry = $"{DateTime.Now}: {transfer.Operation} - {transfer.Source} to {transfer.Target} - {transfer.Status}\n";
            File.AppendAllText(logPath, logEntry);
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
