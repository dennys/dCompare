using dCompare.DirTreeSystem.Models;

namespace dCompare.DirTreeSystem
{
    public class MftFileSystemTreeBuilder : IFileSystemTreeBuilder
    {

        public FileSystemNode BuildTreeRecursive(string path, int depth, CancellationToken cancellationToken, Action? onDirectoryProcessed)
        {
            return new FileSystemNode();
        }
    }
}
