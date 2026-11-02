using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace dCompare.FileOperation.Strategies;

/// <summary>
/// Implements file operations using Windows Kernel32.dll for high performance.
/// </summary>
public class Kernel32FileCopyStrategy : IFileOperationStrategy
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CopyFileEx(
        string lpExistingFileName,
        string lpNewFileName,
        CopyProgressRoutine lpProgressRoutine,
        nint lpData,
        [MarshalAs(UnmanagedType.Bool)] ref bool pbCancel,
        CopyFileFlags dwCopyFlags);

    private delegate CopyProgressResult CopyProgressRoutine(
        long TotalFileSize,
        long TotalBytesTransferred,
        long StreamSize,
        long StreamBytesTransferred,
        uint dwStreamNumber,
        CopyProgressCallbackReason dwCallbackReason,
        nint hSourceFile,
        nint hDestinationFile,
        nint lpData);

    private const int ErrorFileExists = 80; // ERROR_FILE_EXISTS, what CopyFileEx reports for a refused overwrite.

    public Task CopyFileAsync(string sourceFile, string destFile, bool overwrite, CopyProgressCallback? progressCallback, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            bool cancel = false;
            using (cancellationToken.Register(() => cancel = true))
            {
                // CopyFileEx writes straight onto its target, so a cancel or a failure half way through left a
                // truncated file where the user's version was (first-time copies had no temp file at all).
                // Every copy is staged beside the destination and swapped, which is atomic on the same volume.
                string tempFile = destFile + ".dCompare.tmp";

                // Refused up front because staging into the temp file loses COPY_FILE_FAIL_IF_EXISTS, which is
                // the only thing that used to protect an existing destination.
                if (!overwrite && File.Exists(destFile))
                {
                    throw new Win32Exception(ErrorFileExists);
                }

                // Ensure destination directory exists
                string? destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                // The actual P/Invoke delegate needs a different signature.
                // We adapt our public-facing delegate to the required one.
                CopyProgressRoutine routine = (total, transferred, _, _, _, _, _, _, _) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return CopyProgressResult.PROGRESS_CANCEL;
                    }
                    return progressCallback?.Invoke(total, transferred, 0, 0, 0, CopyProgressCallbackReason.CALLBACK_CHUNK_FINISHED, nint.Zero, nint.Zero, nint.Zero)
                           ?? CopyProgressResult.PROGRESS_CONTINUE;
                };

                try
                {
                    FileOperationRetry.Run($"CopyFileEx {sourceFile} -> {tempFile}", () =>
                    {
                        // A retry cannot resume into the partial file the previous attempt left: the
                        // COPY_FILE_FAIL_IF_EXISTS flag makes CopyFileEx refuse an existing target.
                        FileOperationRetry.TryDelete(tempFile);

                        // Read the code before anything else touches the thread's last error (File.Delete does).
                        if (!CopyFileEx(sourceFile, tempFile, routine, nint.Zero, ref cancel, CopyFileFlags.COPY_FILE_FAIL_IF_EXISTS))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                    });

                    FileOperationRetry.Run($"move {tempFile} -> {destFile}", () => File.Move(tempFile, destFile, true));
                }
                catch
                {
                    FileOperationRetry.TryDelete(tempFile);
                    throw;
                }

                // Preserve metadata
                //FileInfo originalFile = new(sourceFile);
                //FileInfo copiedFile = new(destFile)
                //{
                //    CreationTimeUtc = originalFile.CreationTimeUtc,
                //    LastAccessTimeUtc = originalFile.LastAccessTimeUtc,
                //    LastWriteTimeUtc = originalFile.LastWriteTimeUtc
                //};
            }
        }, cancellationToken);
    }

    public void MoveFile(string sourceFile, string destFile, bool overwrite)
    {
        // File.Move preserves metadata on the same volume.
        FileOperationRetry.Run($"move {sourceFile} -> {destFile}", () => File.Move(sourceFile, destFile, overwrite));
    }

    public void DeleteFile(string filePath)
    {
        FileOperationRetry.Run($"delete {filePath}", () => File.Delete(filePath));
    }

    public void DeleteDirectory(string dirPath)
    {
        FileOperationRetry.Run($"delete directory {dirPath}", () => Directory.Delete(dirPath));
    }
}