using dCompare.FileOperation.Models;

namespace dCompare.FileOperation.Messages
{
    public class CompleteTransferMessage
    {
        public FileTransfer Transfer { get; }
        public bool Success { get; }
        public string Message { get; }

        // Cancelled is neither of the two: a half-copied file must not be booked as a completed transfer.
        public bool Cancelled { get; }

        public CompleteTransferMessage(FileTransfer transfer, bool success, string message, bool cancelled = false)
        {
            Transfer = transfer;
            Success = success;
            Message = message;
            Cancelled = cancelled;
        }
    }
}
