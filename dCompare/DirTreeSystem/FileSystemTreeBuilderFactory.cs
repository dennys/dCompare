using dCompare.Common;
using dCompare.Compare;
using System.IO;

namespace dCompare.DirTreeSystem
{
    public static class FileSystemTreeBuilderFactory
    {
        public static IFileSystemTreeBuilder Create(string path, IFileSystem fileSystem)
        {
            if (File.Exists(path) && FileExtensions.IsArchiveFile(path))
            {
                return new ArchiveFileSystemTreeBuilder();
            }
            else
            {
                return new DotnetFileSystemTreeBuilder(fileSystem);
            }
        }
    }
}
