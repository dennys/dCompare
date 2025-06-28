using dCompare.Compare;
using dCompare.FileOperation;
using dCompare.FileOperation.Models;
using dCompare.FileOperation.Strategies;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input; // Added for ICommand

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

        public ObservableCollection<FileTransfer> QueuedTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> SuccessfulTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> FailedTransfers { get; } = new ObservableCollection<FileTransfer>();

        public delegate void TransferStatusChangedHandler(string jobId, bool success, string message);
        public event TransferStatusChangedHandler OnTransferStatusChanged;

        // ICommand properties
        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand CancelTransferCommand { get; }
        public ICommand RetryTransferCommand { get; }
        public ICommand FilterTextChangedCommand { get; }

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

            // Initialize commands
            StartCommand = new RelayCommand(OnStart, CanStart);
            PauseCommand = new RelayCommand(OnPause, CanPause);
            CancelCommand = new RelayCommand(OnCancel, CanCancel);
            CancelTransferCommand = new RelayCommand(OnCancelTransfer, CanCancelTransfer);
            RetryTransferCommand = new RelayCommand(OnRetryTransfer, CanRetryTransfer);
            FilterTextChangedCommand = new RelayCommand(OnFilterTextChanged, CanFilterTextChanged);
        }

        public List<string> CopyMultiple(List<(string Source, string TargetPath, SourceType Type)> items, bool overwrite = false)
        {
            var jobIds = new List<string>();
            foreach (var item in items)
            {
                jobIds.Add(Transfer(item.Source, item.TargetPath, overwrite, true, item.Type));
            }
            return jobIds;
        }

        public List<string> MoveMultiple(List<(string Source, string TargetPath, SourceType Type)> items, bool overwrite = false)
        {
            var jobIds = new List<string>();
            foreach (var item in items)
            {
                jobIds.Add(Transfer(item.Source, item.TargetPath, overwrite, false, item.Type));
            }
            return jobIds;
        }

        public List<string> DeleteMultiple(List<(string Path, SourceType Type)> items)
        {
            var jobIds = new List<string>();
            foreach (var item in items)
            {
                jobIds.Add(Delete(item.Path, item.Type));
            }
            return jobIds;
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

        private string Delete(string path, SourceType sourceType)
        {
            Log.Information($"Start to delete {path}");
            string jobId = Guid.NewGuid().ToString();
            var transfer = new FileTransfer
            {
                JobId = jobId,
                Operation = "Delete",
                Source = path,
                Target = string.Empty,
                Status = "Queued",
                FileSize = 0,
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
                            transfer.Status = $"Deleting ({p.Percentage:P0})";
                        });
                    });

                    while (_isPaused && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }

                    transfer.StartTime = DateTime.Now;

                    OperationResult result = await _fileManager.DeleteAsync(path, progress, _cancellationTokenSource.Token);

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
                FileSize = Directory.Exists(source) ? 0 : new FileInfo(source).Length,
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
                            transfer.Status = $"Transferring ({p.Percentage:P0})";
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

                    OperationResult result;
                    transfer.StartTime = DateTime.Now;

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

        private void LogTransfer(FileTransfer transfer)
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileOperationLog.txt");
            string logEntry = $"{DateTime.Now}: {transfer.Operation} - {transfer.Source} to {transfer.Target} - {transfer.Status}\n";
            File.AppendAllText(logPath, logEntry);
        }

        private bool FilterTransfers(object item)
        {
            if (string.IsNullOrEmpty(FilterTextBox.Text))
                return true;

            var transfer = item as FileTransfer;
            string filter = FilterTextBox.Text.ToLower();
            return transfer.Source.ToLower().Contains(filter) || transfer.Target.ToLower().Contains(filter);
        }

        // Command handlers
        private void OnStart(object parameter)
        {
            // StartButton_Click logic
            _isPaused = false;
        }

        private bool CanStart(object parameter) => _isPaused;

        private void OnPause(object parameter)
        {
            // PauseButton_Click logic
            _isPaused = true;
        }

        private bool CanPause(object parameter) => !_isPaused;

        private void OnCancel(object parameter)
        {
            // CancelButton_Click logic
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private bool CanCancel(object parameter) => QueuedTransfers.Any();

        private void OnCancelTransfer(object parameter)
        {
            // CancelTransfer_Click logic
            if (QueuedGrid.SelectedItem is FileTransfer transfer)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private bool CanCancelTransfer(object parameter) => QueuedGrid.SelectedItem != null;

        private void OnRetryTransfer(object parameter)
        {
            // RetryTransfer_Click logic
            if (FailedGrid.SelectedItem is FileTransfer transfer)
            {
                FailedTransfers.Remove(transfer);
                Transfer(transfer.Source, transfer.Target, true, transfer.Operation == "Copy", transfer.SourceType);
            }
        }

        private bool CanRetryTransfer(object parameter) => FailedGrid.SelectedItem != null;

        private void OnFilterTextChanged(object parameter)
        {
            // FilterTextBox_TextChanged logic
            CollectionViewSource.GetDefaultView(QueuedTransfers).Refresh();
            CollectionViewSource.GetDefaultView(SuccessfulTransfers).Refresh

();
            CollectionViewSource.GetDefaultView(FailedTransfers).Refresh();
        }

        private bool CanFilterTextChanged(object parameter) => true;
    }

    public class FileTransfer : INotifyPropertyChanged
    {
        private string _status;
        private double _progress;
        private double _transferSpeed;

        public string JobId { get; set; }
        public string Operation { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public long FileSize { get; set; }
        public DateTime StartTime { get; set; }
        public SourceType SourceType { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public double TransferSpeed
        {
            get => _transferSpeed;
            set
            {
                _transferSpeed = value;
                OnPropertyChanged(nameof(TransferSpeed));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}