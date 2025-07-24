using dCompare.Common;
using dCompare.DirTreeSystem.Models;
using SharpCompress.Archives;
using System.IO;

namespace dCompare.DirTreeSystem
{
    public class ArchiveFileSystemTreeBuilder : IFileSystemTreeBuilder
    {
        public FileSystemNode BuildTreeRecursive(string path, int depth, CancellationToken cancellationToken, Action? onDirectoryProcessed = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(path) || !FileExtensions.IsArchiveFile(path))
            {
                throw new ArgumentException($"The file '{path}' is not a supported archive format ({string.Join(", ", FileExtensions.ArchiveExtensions)}).");
            }

            FileSystemNode root = new()
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FullPath = path,
                IsDirectory = true,
                Depth = depth
            };

            try
            {
                using (var archive = ArchiveFactory.Open(path))
                {
                    long totalSize = 0;
                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string entryPath = entry.Key?.Replace('/', Path.DirectorySeparatorChar)
                            ?? throw new InvalidOperationException("Entry key is null.");
                        string[] pathParts = entryPath.Split(Path.DirectorySeparatorChar);
                        FileSystemNode currentNode = root;

                        // Build the directory structure
                        for (int i = 0; i < pathParts.Length - 1; i++)
                        {
                            string dirName = pathParts[i];
                            var existingDir = currentNode.Children.FirstOrDefault(c => c.Name == dirName && c.IsDirectory);
                            if (existingDir == null)
                            {
                                existingDir = new FileSystemNode
                                {
                                    Name = dirName,
                                    FullPath = Path.Combine(currentNode.FullPath, dirName),
                                    IsDirectory = true,
                                    Depth = currentNode.Depth + 1
                                };
                                currentNode.Children.Add(existingDir);
                                onDirectoryProcessed?.Invoke();
                            }
                            currentNode = existingDir;
                        }

                        // Add the file
                        string fileName = pathParts[^1];
                        var fileNode = new FileSystemNode
                        {
                            Name = fileName,
                            FullPath = Path.Combine(currentNode.FullPath, fileName),
                            IsDirectory = false,
                            Size = entry.Size,
                            ModifiedTime = entry.LastModifiedTime ?? DateTime.Now,
                            Depth = currentNode.Depth + 1
                        };
                        currentNode.Children.Add(fileNode);
                        totalSize += fileNode.Size;
                    }
                    root.Size = totalSize;
                }
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to process archive '{path}': {ex.Message}", ex);
            }

            return root;
        }
    }
}