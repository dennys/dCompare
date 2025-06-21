using dCompare.Compare;
using dCompare.FileOperation;
using dCompare.FileOperation.Models;
using dCompare.FileOperation.Strategies;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace dCompare.FileOperation
{
    public partial class FileOperationWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isPaused;
        private readonly FileManager _fileManager;

        public ObservableCollection<FileTransfer> QueuedTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> SuccessfulTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> FailedTransfers { get; } = new ObservableCollection<FileTransfer>();

        public delegate void TransferStatusChangedHandler(string jobId, bool success, string message);
        public event TransferStatusChangedHandler OnTransferStatusChanged;

        public FileOperationWindow()
        {
            InitializeComponent();
            DataContext = this;
            _cancellationTokenSource = new CancellationTokenSource();
            _isPaused = false;
            //_fileManager = new FileManager(new Kernel32FileCopyStrategy());
            _fileManager = new FileManager(new FileStreamStrategy());

            var queuedView = CollectionViewSource.GetDefaultView(QueuedTransfers);
            var successfulView = CollectionViewSource.GetDefaultView(SuccessfulTransfers);
            var failedView = CollectionViewSource.GetDefaultView(FailedTransfers);
            queuedView.Filter = FilterTransfers;
            successfulView.Filter = FilterTransfers;
            failedView.Filter = FilterTransfers;
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

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = false;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            _isPaused = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void CancelTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (QueuedGrid.SelectedItem is FileTransfer transfer)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }

        private void RetryTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (FailedGrid.SelectedItem is FileTransfer transfer)
            {
                FailedTransfers.Remove(transfer);
                Transfer(transfer.Source, transfer.Target, true, transfer.Operation == "Copy", transfer.SourceType);
            }
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(QueuedTransfers).Refresh();
            CollectionViewSource.GetDefaultView(SuccessfulTransfers).Refresh();
            CollectionViewSource.GetDefaultView(FailedTransfers).Refresh();
        }
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