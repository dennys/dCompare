using Serilog;
using System.ComponentModel;
using System.IO;

namespace dCompare.FileOperation.Strategies;

/// <summary>
/// Retries the two Win32 errors antivirus, Explorer and OneDrive cause routinely: a file is held for a
/// few hundred milliseconds and the operation is otherwise reported as a hard failure. Anything else —
/// missing file, access denied, disk full — surfaces on the first attempt, unchanged.
/// </summary>
internal static class FileOperationRetry
{
    private const int Attempts = 3;
    private const int BackoffMs = 100;
    private const int SharingViolation = 32;  // ERROR_SHARING_VIOLATION
    private const int LockViolation = 33;     // ERROR_LOCK_VIOLATION

    public static void Run(string operation, Action action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < Attempts && IsTemporaryLock(ex))
            {
                Log.Debug(ex, "{Operation} is momentarily locked; retry {Attempt} of {Attempts}.", operation, attempt, Attempts);
                Thread.Sleep(BackoffMs);
            }
        }
    }

    /// <summary>
    /// Replaces <paramref name="destinationFile"/> with <paramref name="sourceFile"/>.
    /// MoveFileEx refuses to replace a read-only target (ERROR_ACCESS_DENIED), which made "copy over" a
    /// read-only file a hard failure. The bit is lifted only for the duration of the move and put back when
    /// the move did not happen, so a failed copy never leaves the user's original file writable.
    /// </summary>
    public static void MoveWithOverwrite(string sourceFile, string destinationFile, bool overwrite)
    {
        FileAttributes originalAttributes = FileAttributes.Normal;
        bool liftedReadOnly = false;

        if (overwrite && File.Exists(destinationFile))
        {
            originalAttributes = File.GetAttributes(destinationFile);
            if ((originalAttributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(destinationFile, originalAttributes & ~FileAttributes.ReadOnly);
                liftedReadOnly = true;
            }
        }

        try
        {
            Run($"move {sourceFile} -> {destinationFile}", () => File.Move(sourceFile, destinationFile, overwrite));
        }
        catch
        {
            // The destination is still the user's own file: restore what we took off it. Failures here are
            // noise next to the move error the caller is about to report, so they are logged and swallowed.
            if (liftedReadOnly && File.Exists(destinationFile))
            {
                try
                {
                    File.SetAttributes(destinationFile, originalAttributes);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not restore the attributes of '{DestinationFile}'.", destinationFile);
                }
            }

            throw;
        }
    }

    // Best effort: a locked staging file must not replace the error the caller is about to report.
    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                // CopyFileEx stamps the staging file with the source's attributes, and File.Delete refuses a
                // read-only file: without this the leftovers of a failed copy would make every later copy into
                // the same folder die on ERROR_FILE_EXISTS. Only our own .dCompare.tmp files land here.
                try
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Could not clear the attributes of '{Path}'.", path);
                }

                Run($"delete {path}", () => File.Delete(path));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug(ex, "Left {Path} behind.", path);
        }
    }

    // IOException/UnauthorizedAccessException carry the Win32 code in the low word of the HRESULT; the
    // P/Invoke path throws Win32Exception, which has already decoded it.
    private static bool IsTemporaryLock(Exception ex) => ex switch
    {
        Win32Exception win32 => win32.NativeErrorCode is SharingViolation or LockViolation,
        // A Win32 error surfaces in HResult as 0x8007xxxx (facility 7 = FACILITY_WIN32). Without the facility
        // check, a COM (0x8004xxxx) or CLR (0x8013xxxx) error whose low word happens to be 32/33 would be
        // misread as a file lock and burn three needless retries.
        IOException or UnauthorizedAccessException => ex.HResult is unchecked((int)0x8007_0020) or unchecked((int)0x8007_0021),
        _ => false
    };
}
