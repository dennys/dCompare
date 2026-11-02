using Microsoft.VisualBasic.FileIO;
using Serilog;
using System.IO;

namespace dCompare.FileOperation;

/// <summary>
/// Shell-based deletion so an accidental delete stays undoable. The permanent File/Directory.Delete
/// path stays as the fallback for locations that have no recycle bin.
/// UIOption.OnlyErrorDialogs is the .NET 8 spelling of the old OnlyIfErrorOccurred: the shell stays
/// silent unless the delete itself fails.
/// </summary>
internal static class RecycleBin
{
    /// <summary>
    /// Moves a file, or a whole directory tree in one shell operation, to the Recycle Bin.
    /// Returns false when nothing was deleted, so the caller can fall back to a permanent delete.
    /// </summary>
    internal static bool TryDelete(string path, bool isDirectory)
    {
        // Nothing to recycle: the caller's own delete reports the real outcome, and asking the shell would
        // only raise a "file not found" error dialog.
        if (!(isDirectory ? Directory.Exists(path) : File.Exists(path)))
        {
            return false;
        }

        try
        {
            if (isDirectory)
            {
                FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Network shares and a few special paths have no recycle bin; the caller's fallback then
            // destroys the data for good, so the failed attempt has to be visible in the log.
            Log.Warning(ex, "Recycle-bin delete failed for {Path}; falling back to permanent delete.", path);
            return false;
        }
    }
}
