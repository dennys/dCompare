using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.FileOperation
{
    public partial class Operation : Window
    {
        public ObservableCollection<FileTransfer> QueuedTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> SuccessfulTransfers { get; } = new ObservableCollection<FileTransfer>();
        public ObservableCollection<FileTransfer> FailedTransfers { get; } = new ObservableCollection<FileTransfer>();

        public delegate void TransferStatusChangedHandler(string jobId, bool success, string message);
        public event TransferStatusChangedHandler OnTransferStatusChanged;

        public Operation()
        {
            InitializeComponent();
            QueuedGrid.ItemsSource = QueuedTransfers;
            SuccessfulGrid.ItemsSource = SuccessfulTransfers;
            FailedGrid.ItemsSource = FailedTransfers;
        }

        public void Copy(string source, string target, bool overwrite = false)
        {
            Transfer(source, target, overwrite, true);
        }

        public void Move(string source, string target, bool overwrite = false)
        {
            Transfer(source, target, overwrite, false);
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
                Status = "Queued"
            };

            Dispatcher.Invoke(() => QueuedTransfers.Add(transfer));

            Task.Run(async () =>
            {
                try
                {
                    if (isCopy)
                    {
                        File.Copy(source, target, overwrite);
                    }
                    else
                    {
                        File.Move(source, target, overwrite);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        QueuedTransfers.Remove(transfer);
                        transfer.Status = "Completed";
                        SuccessfulTransfers.Add(transfer);
                    });

                    OnTransferStatusChanged?.Invoke(jobId, true, "Operation completed successfully.");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        QueuedTransfers.Remove(transfer);
                        transfer.Status = $"Failed: {ex.Message}";
                        FailedTransfers.Add(transfer);
                    });

                    OnTransferStatusChanged?.Invoke(jobId, false, ex.Message);
                }
            });
        }
    }

    public class FileTransfer : INotifyPropertyChanged
    {
        private string _status;

        public string JobId { get; set; }
        public string Operation { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}