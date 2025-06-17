using System;
using System.IO;
using System.Threading.Tasks;

namespace dCompare.FileOperation.Strategies
{
    /// <summary>
    /// Implements file operations using standard .NET FileStream for copy operations.
    /// </summary>
    public class FileStreamStrategy : IFileOperationStrategy
    {
        public async Task CopyFileAsync(string sourceFile, string destFile, bool overwrite, CopyProgressCallback? progressCallback, CancellationToken cancellationToken)
        {
            FileInfo sourceInfo = new FileInfo(sourceFile);
            long totalBytes = sourceInfo.Length;
            long bytesTransferred = 0;

            using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
            using (var targetStream = new FileStream(destFile, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write))
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
            File.SetCreationTime(destFile, sourceInfo.CreationTime);
            File.SetLastAccessTime(destFile, sourceInfo.LastAccessTime);
            File.SetLastWriteTime(destFile, sourceInfo.LastWriteTime);
        }

        public void MoveFile(string sourceFile, string destFile, bool overwrite)
        {
            if (overwrite && File.Exists(destFile))
            {
                File.Delete(destFile);
            }
            File.Move(sourceFile, destFile);
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
}