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
                throw new ArgumentException(string.Format(Properties.Resources.UnsupportedArchiveFormat, path, string.Join(", ", FileExtensions.ArchiveExtensions)));
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
                using (var archive = ArchiveFactory.OpenArchive(path))
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
                            // Case-insensitive, like the comparison itself: entry keys are not normalised, so a zip
                            // made on Linux can hold both "Docs/" and "docs/". Matching case-sensitively built two
                            // sibling nodes, and the comparer (which is case-insensitive) then visited only the first
                            // one, so the files under the other never appeared in the results at all.
                            var existingDir = currentNode.Children.FirstOrDefault(c => c.IsDirectory
                                && c.Name.Equals(dirName, StringComparison.OrdinalIgnoreCase));
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
                            // ZIP stores 1980-based fields; reading both sides the same way (UTC) is what lets two
                            // byte-identical archives agree. A missing stamp is MinValue, never Now: "now" differs
                            // between the two scans and would report every entry as Modified.
                            ModifiedTime = entry.LastModifiedTime ?? DateTime.MinValue,
                            // FullPath above is archive file + entry key, a path nothing can open, so content
                            // identity has to come from the entry metadata. SharpCompress reports 0 when the
                            // format carries no CRC (tar, some 7z solid entries), which means "unknown".
                            IsArchiveEntry = true,
                            Crc = entry.Crc == 0 ? null : entry.Crc,
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
                throw new IOException(string.Format(Properties.Resources.FailedToProcessArchive, path, ex.Message), ex);
            }

            return root;
        }
    }
}