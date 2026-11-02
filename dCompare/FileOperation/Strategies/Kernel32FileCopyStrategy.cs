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

    public Task CopyFileAsync(string sourceFile, string destFile, bool overwrite, CopyProgressCallback? progressCallback, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            bool cancel = false;
            using (cancellationToken.Register(() => cancel = true))
            {
                CopyFileFlags flags = overwrite ? 0 : CopyFileFlags.COPY_FILE_FAIL_IF_EXISTS;

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

                if (!CopyFileEx(sourceFile, destFile, routine, nint.Zero, ref cancel, flags))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
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
        File.Move(sourceFile, destFile, overwrite);  // File.Move preserves metadata on the same volume.
    }

    public void DeleteFile(string filePath)
    {
        File.Delete(filePath);
    }

    public void DeleteDirectory(string dirPath)
    {
        Directory.Delete(dirPath);
    }
}