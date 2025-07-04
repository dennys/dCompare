namespace dCompare.FileOperation.Strategies;

/// <summary>
/// Defines a strategy for performing file operations.
/// </summary>
public interface IFileOperationStrategy
{
    /// <summary>
    /// Copies a file.
    /// </summary>
    Task CopyFileAsync(string sourceFile, string destFile, bool overwrite, CopyProgressCallback? progressCallback, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a file.
    /// </summary>
    void MoveFile(string sourceFile, string destFile, bool overwrite);

    /// <summary>
    /// Deletes a file.
    /// </summary>
    void DeleteFile(string filePath);

    /// <summary>
    /// Deletes an empty directory.
    /// </summary>
    void DeleteDirectory(string dirPath);
}

// Delegate for CopyFileEx progress routine
public delegate CopyProgressResult CopyProgressCallback(
    long totalFileSize,
    long totalBytesTransferred,
    long streamSize,
    long streamBytesTransferred,
    uint dwStreamNumber,
    CopyProgressCallbackReason dwCallbackReason,
    nint hSourceFile,
    nint hDestinationFile,
    nint lpData);

[Flags]
public enum CopyFileFlags : uint
{
    COPY_FILE_FAIL_IF_EXISTS = 0x00000001,
    COPY_FILE_RESTARTABLE = 0x00000002, // Not recommended for large file transfers
    COPY_FILE_OPEN_SOURCE_FOR_WRITE = 0x00000004,
    COPY_FILE_ALLOW_DECRYPTED_DESTINATION = 0x00000008,
    COPY_FILE_COPY_SYMLINK = 0x00000800,
    COPY_FILE_NO_BUFFERING = 0x00001000
}

public enum CopyProgressResult : uint
{
    PROGRESS_CONTINUE = 0,
    PROGRESS_CANCEL = 1,
    PROGRESS_STOP = 2,
    PROGRESS_QUIET = 3
}

public enum CopyProgressCallbackReason : uint
{
    CALLBACK_CHUNK_FINISHED = 0x00000000,
    CALLBACK_STREAM_SWITCH = 0x00000001
}