using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Serilog;

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
