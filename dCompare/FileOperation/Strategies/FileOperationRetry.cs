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

    // Best effort: a locked staging file must not replace the error the caller is about to report.
    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
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
