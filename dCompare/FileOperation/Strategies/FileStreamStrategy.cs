using Serilog;
using System.IO;
using System.Security.AccessControl;

namespace dCompare.FileOperation.Strategies
{
    /// <summary>
    /// Implements file operations using standard .NET FileStream for copy operations.
    /// </summary>
    public class FileStreamStrategy : IFileOperationStrategy
    {
        public async Task CopyFileAsync(string sourceFile, string destFile, bool overwrite, CopyProgressCallback? progressCallback, CancellationToken cancellationToken)
        {
            FileInfo sourceInfo = new(sourceFile);
            long totalBytes = sourceInfo.Length;
            long bytesTransferred = 0;

            // Ensure destination directory exists
            string? destDirectory = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destDirectory))
            {
                Directory.CreateDirectory(destDirectory);
            }

            // The destination is never opened for writing: a cancel, disk-full or exception half way through
            // would leave a truncated file in place of the user's version. Fill a temp file, stamp it, swap it in.
            string tempFile = destFile + ".dCompare.tmp";
            FileOperationRetry.TryDelete(tempFile);

            try
            {
                using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
                using (var targetStream = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write))
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await targetStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        bytesTransferred += bytesRead;

                        if (progressCallback != null && totalBytes > 0)
                        {
                            var result = progressCallback(totalBytes, bytesTransferred, 0, 0, 0, CopyProgressCallbackReason.CALLBACK_CHUNK_FINISHED, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                            if (result == CopyProgressResult.PROGRESS_CANCEL)
                            {
                                throw new OperationCanceledException();
                            }
                        }
                    }
                }

                // Preserve original file timestamps
                File.SetCreationTime(tempFile, sourceInfo.CreationTime);
                File.SetLastAccessTime(tempFile, sourceInfo.LastAccessTime);
                File.SetLastWriteTime(tempFile, sourceInfo.LastWriteTime);

                // A read-only destination would block MoveFileEx's replace (ERROR_ACCESS_DENIED on a
                // second copy); MoveWithOverwrite lifts the attribute for the move and puts it back if it fails.
                FileOperationRetry.MoveWithOverwrite(tempFile, destFile, overwrite);

                // Attributes must land on the final file (the temp file is always Normal);
                // without this a read-only source copies as a writable file.
                File.SetAttributes(destFile, sourceInfo.Attributes);
                CopyAccessControlBestEffort(sourceFile, destFile);
            }
            catch
            {
                FileOperationRetry.TryDelete(tempFile);
                throw;
            }
        }

        public void MoveFile(string sourceFile, string destFile, bool overwrite)
        {
            // File.Move(…, overwrite) replaces the target in one step; deleting the target first lost both
            // copies whenever the move itself then failed (locked source, cross-volume hiccup, ACL).
            FileOperationRetry.MoveWithOverwrite(sourceFile, destFile, overwrite);
        }

        public void DeleteFile(string filePath)
        {
            FileOperationRetry.Run($"delete {filePath}", () => File.Delete(filePath));
        }

        public void DeleteDirectory(string dirPath)
        {
            FileOperationRetry.Run($"delete directory {dirPath}", () => Directory.Delete(dirPath));
        }

        // ACLs are best-effort: GetAccessControl throws PlatformNotSupportedException on some file
        // systems, and a copy must never fail because of them.
        private static void CopyAccessControlBestEffort(string sourceFile, string destFile)
        {
            try
            {
                var accessRule = new FileInfo(sourceFile).GetAccessControl(AccessControlSections.Access);
                new FileInfo(destFile).SetAccessControl(accessRule);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Best-effort ACL copy skipped for '{DestFile}'.", destFile);
            }
        }
    }
}