using dCompare.Common;
using dCompare.FileOperation.Models;
using dCompare.FileOperation.Strategies;
using dCompare.Infrastructure;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace dCompare.FileOperation
{
    public static class TextBoxBehaviors
    {
        public static readonly DependencyProperty TextChangedCommandProperty =
            DependencyProperty.RegisterAttached("TextChangedCommand", typeof(ICommand), typeof(TextBoxBehaviors),
                new UIPropertyMetadata(null, OnTextChangedCommandChanged));

        public static ICommand GetTextChangedCommand(DependencyObject obj) =>
            (ICommand)obj.GetValue(TextChangedCommandProperty);

        public static void SetTextChangedCommand(DependencyObject obj, ICommand value) =>
            obj.SetValue(TextChangedCommandProperty, value);

        private static void OnTextChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if (e.OldValue == null && e.NewValue != null)
                    textBox.TextChanged += TextBox_TextChanged;
                else if (e.OldValue != null && e.NewValue == null)
                    textBox.TextChanged -= TextBox_TextChanged;
            }
        }

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var command = GetTextChangedCommand(textBox);
                if (command != null && command.CanExecute(e))
                    command.Execute(e);
            }
        }
    }
    public partial class FileOperationControl : UserControl
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isPaused;
        private readonly FileManager _fileManager;

        public ObservableCollection<FileTransfer> QueuedTransfers { get; } = new();
        public ObservableCollection<FileTransfer> SuccessfulTransfers { get; } = new();
        public ObservableCollection<FileTransfer> FailedTransfers { get; } = new();

        public delegate void TransferStatusChangedHandler(string jobId, bool success, string message);
        public event TransferStatusChangedHandler? OnTransferStatusChanged;

        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CancelTransferCommand { get; }
        public ICommand RetryTransferCommand { get; }
        public ICommand FilterTextChangedCommand { get; }
        public ICommand ClearQueuedCommand { get; }
        public ICommand ClearSuccessfulCommand { get; }
        public ICommand ClearFailedCommand { get; }

        public FileOperationControl()
        {
            InitializeComponent();
            DataContext = this;
            _cancellationTokenSource = new CancellationTokenSource();
            _isPaused = false;
            _fileManager = new FileManager(new Kernel32FileCopyStrategy());

            var queuedView = CollectionViewSource.GetDefaultView(QueuedTransfers);
            var successfulView = CollectionViewSource.GetDefaultView(SuccessfulTransfers);
            var failedView = CollectionViewSource.GetDefaultView(FailedTransfers);
            queuedView.Filter = FilterTransfers;
            successfulView.Filter = FilterTransfers;
            failedView.Filter = FilterTransfers;

            StartCommand = new RelayCommand(OnStart, CanStart);
            PauseCommand = new RelayCommand(OnPause, CanPause);
            CancelCommand = new RelayCommand(OnCancel, CanCancel);
            CancelTransferCommand = new RelayCommand(OnCancelTransfer, CanCancelTransfer);
            RetryTransferCommand = new RelayCommand(OnRetryTransfer, CanRetryTransfer);
            FilterTextChangedCommand = new RelayCommand(OnFilterTextChanged, CanFilterTextChanged);
            ClearQueuedCommand = new RelayCommand(OnClearQueued, CanClearQueued);
            ClearSuccessfulCommand = new RelayCommand(OnClearSuccessful, CanClearSuccessful);
            ClearFailedCommand = new RelayCommand(OnClearFailed, CanClearFailed);
        }

        public void ClearQueuedTransfers()
        {
            Dispatcher.Invoke(() => QueuedTransfers.Clear());
        }

        public void ClearSuccessfulTransfers()
        {
            Dispatcher.Invoke(() => SuccessfulTransfers.Clear());
        }

        public void ClearFailedTransfers()
        {
            Dispatcher.Invoke(() => FailedTransfers.Clear());
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

        private string Transfer(string source, string target, bool overwrite, bool isCopy, SourceType sourceType)
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

            Dispatcher.Invoke(() => QueuedTransfers.Add(transfer));

            Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<OperationProgress>(p =>
                    {
                        Dispatcher.Invoke(() =>
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
                        Dispatcher.Invoke(() =>
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
                    Dispatcher.Invoke(() =>
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
                    Dispatcher.Invoke(() =>
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
            if (string.IsNullOrEmpty(FilterTextBox?.Text))
                return true;

            var transfer = item as FileTransfer;
            string filter = FilterTextBox.Text.ToLowerInvariant();
            return transfer?.Source?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true ||
                   transfer?.Target?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;
        }

        private void OnStart(object parameter)
        {
            _isPaused = false;
        }

        private bool CanStart(object parameter) => _isPaused;

        private void OnPause(object parameter)
        {
            _isPaused = true;
        }

        private bool CanPause(object parameter) => !_isPaused;

        private void OnCancel(object parameter)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private bool CanCancel(object parameter) => QueuedTransfers.Any();

        private void OnCancelTransfer(object parameter)
        {
            if (QueuedGrid.SelectedItem is FileTransfer)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private bool CanCancelTransfer(object parameter) => QueuedGrid.SelectedItem != null;

        private void OnRetryTransfer(object parameter)
        {
            if (FailedGrid.SelectedItem is FileTransfer transfer)
            {
                FailedTransfers.Remove(transfer);
                Transfer(transfer.Source, transfer.Target, true, transfer.Operation == "Copy", transfer.SourceType);
            }
        }

        private bool CanRetryTransfer(object parameter) => FailedGrid.SelectedItem != null;

        private void OnFilterTextChanged(object parameter)
        {
            CollectionViewSource.GetDefaultView(QueuedTransfers).Refresh();
            CollectionViewSource.GetDefaultView(SuccessfulTransfers).Refresh();
            CollectionViewSource.GetDefaultView(FailedTransfers).Refresh();
        }

        private bool CanFilterTextChanged(object parameter) => true;

        private void OnClearQueued(object parameter)
        {
            ClearQueuedTransfers();
        }

        private bool CanClearQueued(object parameter) => QueuedTransfers.Any();

        private void OnClearSuccessful(object parameter)
        {
            ClearSuccessfulTransfers();
        }

        private bool CanClearSuccessful(object parameter) => SuccessfulTransfers.Any();

        private void OnClearFailed(object parameter)
        {
            ClearFailedTransfers();
        }

        private bool CanClearFailed(object parameter) => FailedTransfers.Any();
    }
}