using dCompare.Common;
using System.ComponentModel;
using System.Threading;

namespace dCompare.FileOperation.Models
{
    public class FileTransfer : INotifyPropertyChanged
    {
        private FileOperationStatus _status;
        private double _progress;
        private double _transferSpeed;

        public required string JobId { get; set; }
        public FileOperationTypes Operation { get; set; }
        public required string Source { get; set; }
        public required string Target { get; set; }
        public long FileSize { get; set; }
        public DateTime StartTime { get; set; }
        public SourceType SourceType { get; set; }

        // The token that stops exactly this transfer. Set by whoever starts the work (linked to the batch
        // token, so a batch/global cancel still wins) and read by the operation panel's per-row Cancel.
        public CancellationTokenSource? Cancellation { get; set; }

        public void CancelTransfer()
        {
            try
            {
                Cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The transfer already finished and disposed its token; nothing left to cancel.
            }
        }

        public FileOperationStatus Status
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}