using dCompare.FileOperation.Models;

namespace dCompare.FileOperation.Messages
{
    public class CompleteTransferMessage
    {
        public FileTransfer Transfer { get; }
        public bool Success { get; }
        public string Message { get; }

        public CompleteTransferMessage(FileTransfer transfer, bool success, string message)
        {
            Transfer = transfer;
            Success = success;
            Message = message;
        }
    }
}
