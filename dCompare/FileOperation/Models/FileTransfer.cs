using dCompare.Common;
using System.ComponentModel;

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