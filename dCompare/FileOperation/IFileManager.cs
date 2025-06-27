using dCompare.Compare;
using dCompare.FileOperation.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace dCompare.FileOperation
{
    public interface IFileManager
    {
        void Pause();
        void Resume();
        Task<OperationResult> CopyAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> MoveAsync(string source, string destination, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> DeleteAsync(string path, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> CopyMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> MoveMultipleAsync(List<(string source, string target, SourceType type)> items, bool overwrite = false, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
        Task<OperationResult> DeleteMultipleAsync(List<string> paths, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}