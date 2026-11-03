using dCompare.Common;
using dCompare.Compare;
using dCompare.DirTreeSystem.Models;
using Serilog;
using System.IO;

namespace dCompare.DirTreeSystem
{
    public class DotnetFileSystemTreeBuilder(IFileSystem fileSystem) : IFileSystemTreeBuilder
    {
        private readonly IFileSystem _fileSystem = fileSystem;
        public FileSystemNode BuildTreeRecursive(string path, int depth, CancellationToken cancellationToken, Action? onDirectoryProcessed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileSystemNode root = new()
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = _fileSystem.DirectoryExists(path),
                Depth = depth
            };

            if (root.IsDirectory && _fileSystem.DirectoryExists(path))
            {
                root.ModifiedTime = _fileSystem.GetLastWriteTime(path);
                long totalSize = 0;

                // Each enumeration is listed and consumed separately, and every item is read on its own, so one
                // locked or half-removed entry cannot silently drop the siblings after it. Whatever is lost is
                // recorded on ScanIncomplete and the row is reported as Conflict instead of "missing on one side".
                string[] subDirs = ListEntries(() => _fileSystem.GetDirectories(path), path, "directories", root);
                foreach (string dir in subDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (FileExtensions.IsReparsePoint(dir)) continue;
                    try
                    {
                        FileSystemNode childDir = BuildTreeRecursive(dir, depth + 1, cancellationToken, onDirectoryProcessed);
                        root.Children.Add(childDir);
                        totalSize += childDir.Size;
                        if (childDir.ScanIncomplete) root.ScanIncomplete = true;
                    }
                    catch (Exception ex) when (IsScanFailure(ex))
                    {
                        root.ScanIncomplete = true;
                        Log.Debug(ex, "Cannot scan directory '{Dir}'.", dir);
                    }
                    onDirectoryProcessed?.Invoke();
                }

                string[] files = ListEntries(() => _fileSystem.GetFiles(path), path, "files", root);
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        FileInfo fileInfo = _fileSystem.GetFileInfo(file);
                        FileSystemNode childFile = new()
                        {
                            Name = fileInfo.Name,
                            FullPath = file,
                            IsDirectory = false,
                            Size = fileInfo.Length,
                            ModifiedTime = fileInfo.LastWriteTime,
                            Depth = depth + 1
                        };
                        root.Children.Add(childFile);
                        totalSize += childFile.Size;
                    }
                    catch (Exception ex) when (IsScanFailure(ex))
                    {
                        // The file existed a moment ago and cannot be read now (removed, locked, too long).
                        // Still list it, with no size or date, so it is never mistaken for absent.
                        root.Children.Add(new FileSystemNode
                        {
                            Name = Path.GetFileName(file),
                            FullPath = file,
                            IsDirectory = false,
                            Depth = depth + 1
                        });
                        root.ScanIncomplete = true;
                        Log.Debug(ex, "Cannot read file information for '{File}'.", file);
                    }
                }
                root.Size = totalSize;
            }
            else if (_fileSystem.FileExists(path))
            {
                FileInfo info = _fileSystem.GetFileInfo(path);
                root.Size = info.Length;
                root.ModifiedTime = info.LastWriteTime;
            }

            return root;
        }

        // Anything except cancellation is a read problem we can survive: the walk reports what it could read.
        private static bool IsScanFailure(Exception ex) => ex is not OperationCanceledException;

        private static string[] ListEntries(Func<IEnumerable<string>> list, string path, string what, FileSystemNode root)
        {
            try
            {
                return list().ToArray();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                root.ScanIncomplete = true;
                Log.Debug(ex, "Cannot list {What} of '{Path}'.", what, path);
                return Array.Empty<string>();
            }
        }
    }
}