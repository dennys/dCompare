using dCompare.FileOperation.Models;

namespace dCompare.FileOperation.Messages
{
    public class QueueTransferMessage
    {
        public FileTransfer Transfer { get; }

        public QueueTransferMessage(FileTransfer transfer)
        {
            Transfer = transfer;
        }
    }
}
