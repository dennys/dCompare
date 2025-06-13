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
    public partial class Operation : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isPaused;

        public ObservableCollection<FileTransfer> QueuedTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> SuccessfulTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> FailedTransfers { get; } = new ObservableCollection<FileTransfer>();

        public delegate void TransferStatusChangedHandler(string jobId, bool success, string message);
        public event TransferStatusChangedHandler OnTransferStatusChanged;

        public Operation()
        {
            InitializeComponent();
            DataContext = this;
            _cancellationTokenSource = new CancellationTokenSource();
            _isPaused = false;

            // Set up CollectionViewSource for filtering
            var queuedView = CollectionViewSource.GetDefaultView(QueuedTransfers);
            var successfulView = CollectionViewSource.GetDefaultView(SuccessfulTransfers);
            var failedView = CollectionViewSource.GetDefaultView(FailedTransfers);
            queuedView.Filter = FilterTransfers;
            successfulView.Filter = FilterTransfers;
            failedView.Filter = FilterTransfers;
        }

        public void Copy(string source, string target, bool overwrite = false)
        {
            Transfer(source, target, overwrite, true);
        }

        public void Move(string source, string target, bool overwrite = false)
        {
            Transfer(source, target, overwrite, false);
        }

        public void CopyMultiple(string[] sources, string targetDirectory, bool overwrite = false)
        {
            foreach (var source in sources)
            {
                string target = Path.Combine(targetDirectory, Path.GetFileName(source));
                Copy(source, target, overwrite);
            }
        }

        public void MoveMultiple(string[] sources, string targetDirectory, bool overwrite = false)
        {
            foreach (var source in sources)
            {
                string target = Path.Combine(targetDirectory, Path.GetFileName(source));
                Move(source, target, overwrite);
            }
        }

        private void Transfer(string source, string target, bool overwrite, bool isCopy)
        {
            string jobId = Guid.NewGuid().ToString();
            var transfer = new FileTransfer
            {
                JobId = jobId,
                Operation = isCopy ? "Copy" : "Move",
                Source = source,
                Target = target,
                Status = "Queued",
                FileSize = new FileInfo(source).Length
            };

            Dispatcher.Invoke(() => QueuedTransfers.Add(transfer));

            Task.Run(async () =>
            {
                try
                {
                    long bytesTransferred = 0;
                    long totalBytes = transfer.FileSize;
                    DateTime startTime = DateTime.Now;

                    using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read))
                    using (var targetStream = new FileStream(target, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token)) > 0)
                        {
                            if (_isPaused)
                            {
                                while (_isPaused && !_cancellationTokenSource.Token.IsCancellationRequested)
                                {
                                    await Task.Delay(100);
                                }
                            }

                            await targetStream.WriteAsync(buffer, 0, bytesRead, _cancellationTokenSource.Token);
                            bytesTransferred += bytesRead;

                            double progress = (double)bytesTransferred / totalBytes;
                            double elapsedSeconds = (DateTime.Now - startTime).TotalSeconds;
                            double speed = elapsedSeconds > 0 ? (bytesTransferred / 1024.0) / elapsedSeconds : 0;

                            Dispatcher.Invoke(() =>
                            {
                                transfer.Progress = progress;
                                transfer.TransferSpeed = speed;
                                transfer.Status = $"Transferring ({progress:P0})";
                            });
                        }
                    }

                    if (!isCopy)
                    {
                        File.Delete(source);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        QueuedTransfers.Remove(transfer);
                        transfer.Status = "Completed";
                        SuccessfulTransfers.Add(transfer);
                        LogTransfer(transfer);
                    });

                    OnTransferStatusChanged?.Invoke(jobId, true, "Operation completed successfully.");
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
                Transfer(transfer.Source, transfer.Target, true, transfer.Operation == "Copy");
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