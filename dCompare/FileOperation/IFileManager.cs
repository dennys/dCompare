using dCompare.Common;
using dCompare.FileOperation.Models;

namespace dCompare.FileOperation
{
    public interface IFileManager
    {
        void WhoAmI();
        void Pause();
        void Resume();
        // Stops every batch this manager is running, whichever caller started it.
        void Cancel();
        Task<OperationResult> CopyAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> MoveAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> DeleteAsync(string path, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> CopyMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> MoveMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> DeleteMultipleAsync(List<string> paths, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}