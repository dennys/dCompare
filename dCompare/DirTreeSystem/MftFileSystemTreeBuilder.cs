using dCompare.DirTreeSystem.Models;

namespace dCompare.DirTreeSystem
{
    public class MftFileSystemTreeBuilder : IFileSystemTreeBuilder
    {

        public FileSystemNode BuildTreeRecursive(string path, int depth, CancellationToken cancellationToken, Action? onDirectoryProcessed)
        {
            // Never return an empty tree: a compare that sees nothing reports "everything identical",
            // which invites the user to delete real files. Wire up a real MFT reader before enabling this.
            throw new NotImplementedException("MFT scanning is not implemented; the Options 'MFT' scan method is a placeholder.");
        }
    }
}
