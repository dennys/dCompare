using dCompare.Compare;
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

                try
                {
                    // Process directories
                    foreach (string dir in _fileSystem.GetDirectories(path))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        FileSystemNode childDir = BuildTreeRecursive(dir, depth + 1, cancellationToken, onDirectoryProcessed);
                        root.Children.Add(childDir);
                        totalSize += childDir.Size;
                        onDirectoryProcessed?.Invoke();
                    }

                    // Process files
                    foreach (string file in _fileSystem.GetFiles(path))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        //FileSystemNode childFile = BuildTreeRecursive(file, depth + 1, cancellationToken, onDirectoryProcessed);
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
                    root.Size = totalSize;
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }
            else if (_fileSystem.FileExists(path))
            {
                FileInfo info = _fileSystem.GetFileInfo(path);
                root.Size = info.Length;
                root.ModifiedTime = info.LastWriteTime;
            }

            return root;
        }
    }
}