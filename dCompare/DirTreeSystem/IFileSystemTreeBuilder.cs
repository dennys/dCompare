using dCompare.DirTreeSystem.Models;

namespace dCompare.DirTreeSystem
{
    /// <summary>
    /// Defines the contract for a file system tree builder.
    /// </summary>
    public interface IFileSystemTreeBuilder
    {
        /// <summary>
        /// Builds a hierarchical tree of files and directories from a given path.
        /// </summary>
        /// <param name="path">The root path to start building the tree from.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <param name="onDirectoryProcessed">An optional action to invoke after processing a directory.</param>
        /// <returns>The root node of the file system tree.</returns>
        //FileSystemNode BuildTree(string path, CancellationToken cancellationToken, Action? onDirectoryProcessed = null);
        FileSystemNode BuildTreeRecursive(string path, int depth, CancellationToken cancellationToken, Action? onDirectoryProcessed = null);
    }
}