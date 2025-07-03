using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using Serilog;

namespace dCompare.DirTreeSystem
{
    /// <summary>
    /// Builds a file system tree by reading the NTFS Master File Table (MFT) directly.
    /// This method is very fast but requires administrative privileges.
    /// Includes detailed logging with Serilog.
    /// </summary>
    public class MftFileSystemTreeBuilder : IFileSystemTreeBuilder
    {
        private static readonly ILogger _log = Log.ForContext<MftFileSystemTreeBuilder>();

        public FileSystemNode BuildTreeRecursive(string path, int depth, CancellationToken cancellationToken, Action? onDirectoryProcessed)
        {
            _log.Information("Starting MFT build for path: {Path} at depth {Depth}", path, depth);
            try
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    _log.Error("The specified path does not exist: {Path}", path);
                    throw new DirectoryNotFoundException($"The specified path does not exist: {path}");
                }

                var rootPath = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(rootPath))
                {
                    _log.Error("Could not determine the volume for the given path: {Path}", path);
                    throw new ArgumentException("Could not determine the volume for the given path.", nameof(path));
                }

                var driveLetter = rootPath.Substring(0, 1);
                _log.Debug("Determined drive letter: {DriveLetter}", driveLetter);

                _log.Information("Reading all MFT entries for volume {DriveLetter}:...", driveLetter);
                var allNodes = ReadMft(driveLetter, cancellationToken);
                _log.Information("Finished reading MFT. Found {NodeCount} entries.", allNodes.Count);

                _log.Information("Reconstructing tree from flat list...");
                var rootNode = BuildTreeFromFlatList(allNodes, path, onDirectoryProcessed);
                _log.Information("Successfully reconstructed tree.");

                _log.Debug("Adjusting node depths starting from {Depth}", depth);
                AdjustDepth(rootNode, depth);

                return rootNode;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "An unhandled exception occurred in BuildTreeRecursive for path {Path}", path);
                throw;
            }
        }

        private void AdjustDepth(FileSystemNode node, int depth)
        {
            node.Depth = depth;
            foreach (var child in node.Children)
            {
                AdjustDepth(child, depth + 1);
            }
        }

        private Dictionary<long, (FileSystemNode Node, long ParentFrn)> ReadMft(string driveLetter, CancellationToken cancellationToken)
        {
            var nodes = new Dictionary<long, (FileSystemNode, long)>();
            _log.Debug("Attempting to get handle for volume {DriveLetter}:", driveLetter);
            using (SafeFileHandle volumeHandle = GetVolumeHandle(driveLetter))
            {
                if (volumeHandle.IsInvalid)
                {
                    var error = Marshal.GetLastWin32Error();
                    _log.Error("Could not open handle to volume '{DriveLetter}:'. Win32 error code: {Error}. Make sure you are running as an administrator.", driveLetter, error);
                    throw new Win32Exception(error, $"Could not open handle to volume '{driveLetter}:'. Make sure you are running as an administrator.");
                }
                _log.Debug("Successfully obtained volume handle for {DriveLetter}:", driveLetter);

                var mftEnumData = new MftApi.MFT_ENUM_DATA_V0 { StartFileReferenceNumber = 0, LowUsn = 0, HighUsn = long.MaxValue };
                IntPtr mftEnumDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(mftEnumData));
                Marshal.StructureToPtr(mftEnumData, mftEnumDataPtr, false);

                int bufferSize = 64 * 1024; // 64KB buffer
                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
                bool sizeWarningLogged = false;

                try
                {
                    _log.Debug("Starting DeviceIoControl loop to enumerate USN data.");
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!MftApi.DeviceIoControl(volumeHandle, MftApi.FSCTL_ENUM_USN_DATA, mftEnumDataPtr, (uint)Marshal.SizeOf(mftEnumData), buffer, (uint)bufferSize, out uint bytesReturned, IntPtr.Zero))
                        {
                            int error = Marshal.GetLastWin32Error();
                            if (error == MftApi.ERROR_NO_MORE_FILES || error == MftApi.ERROR_HANDLE_EOF)
                            {
                                _log.Information("Finished enumerating USN data (end of file).");
                                break;
                            }
                            _log.Error("DeviceIoControl failed while enumerating USN data. Win32 error code: {Error}", error);
                            throw new Win32Exception(error, "Failed to enumerate USN data from MFT.");
                        }

                        if (bytesReturned <= MftApi.USN_RECORD_COMMON_HEADER_SIZE)
                        {
                            _log.Debug("DeviceIoControl returned {BytesReturned} bytes, which is not enough for a record. Ending enumeration.", bytesReturned);
                            break;
                        }

                        IntPtr currentUsnRecordPtr = IntPtr.Add(buffer, (int)MftApi.USN_RECORD_COMMON_HEADER_SIZE);
                        long recordsToProcess = bytesReturned - MftApi.USN_RECORD_COMMON_HEADER_SIZE;
                        _log.Verbose("Processing {Bytes} bytes of USN data.", recordsToProcess);

                        while (recordsToProcess > 0)
                        {
                            var usnRecord = Marshal.PtrToStructure<MftApi.USN_RECORD_V2>(currentUsnRecordPtr);

                            // *** BUG FIX: Correctly read the filename using the offset and length ***
                            string fileName = Marshal.PtrToStringUni(
                                IntPtr.Add(currentUsnRecordPtr, (int)usnRecord.FileNameOffset),
                                usnRecord.FileNameLength / 2); // Length is in bytes, PtrToStringUni needs char count

                            if (!sizeWarningLogged)
                            {
                                _log.Warning("File size information is not available via USN_RECORD_V2. All file and directory sizes will be reported as 0.");
                                sizeWarningLogged = true;
                            }

                            var node = new FileSystemNode
                            {
                                Name = fileName,
                                FullPath = string.Empty, // Will be constructed later
                                IsDirectory = (usnRecord.FileAttributes & (uint)FileAttributes.Directory) != 0,
                                ModifiedTime = DateTime.FromFileTime(usnRecord.TimeStamp),
                                Size = 0 // *** BUG FIX: Size is not available in this record, so we set to 0 ***
                            };

                            nodes[usnRecord.FileReferenceNumber] = (node, usnRecord.ParentFileReferenceNumber);

                            if (usnRecord.RecordLength <= 0)
                            {
                                _log.Warning("Invalid record length {Length} encountered. Breaking out of inner loop to avoid infinite loop.", usnRecord.RecordLength);
                                recordsToProcess = 0;
                                continue;
                            }

                            recordsToProcess -= usnRecord.RecordLength;
                            currentUsnRecordPtr = IntPtr.Add(currentUsnRecordPtr, (int)usnRecord.RecordLength);
                        }

                        long nextStartFrn = Marshal.ReadInt64(buffer, 0);
                        mftEnumData.StartFileReferenceNumber = nextStartFrn;
                        Marshal.StructureToPtr(mftEnumData, mftEnumDataPtr, false);
                        _log.Verbose("Next enumeration will start at FRN: {NextFrn}", nextStartFrn);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                    Marshal.FreeHGlobal(mftEnumDataPtr);
                }
            }
            return nodes;
        }

        private FileSystemNode BuildTreeFromFlatList(Dictionary<long, (FileSystemNode Node, long ParentFrn)> allNodes, string targetPath, Action? onDirectoryProcessed)
        {
            var tree = new Dictionary<long, FileSystemNode>();

            _log.Debug("First pass: Cloning {NodeCount} nodes into the tree dictionary.", allNodes.Count);
            foreach (var kvp in allNodes)
            {
                tree[kvp.Key] = kvp.Value.Node;
            }

            _log.Debug("Second pass: Linking children to parents.");
            foreach (var kvp in allNodes)
            {
                long fileRef = kvp.Key;
                long parentFileRef = kvp.Value.ParentFrn;

                if (tree.TryGetValue(parentFileRef, out FileSystemNode parentNode) && fileRef != parentFileRef)
                {
                    parentNode.Children.Add(tree[fileRef]);
                    onDirectoryProcessed?.Invoke();
                }
            }

            FileSystemNode? volumeRoot = null;
            const long NTFS_ROOT_FRN = 5;
            if (tree.TryGetValue(NTFS_ROOT_FRN, out var rootCand))
            {
                volumeRoot = rootCand;
                string rootDrive = Path.GetPathRoot(targetPath) ?? "\\";
                _log.Debug("Found volume root at FRN {RootFrn}. Building full paths starting from '{RootDrive}'.", NTFS_ROOT_FRN, rootDrive);
                BuildFullPathsAndSizes(volumeRoot, rootDrive.TrimEnd('\\'));
            }
            else
            {
                _log.Error("Could not find volume root (FRN {RootFrn}) in the MFT data.", NTFS_ROOT_FRN);
                throw new InvalidOperationException("Could not find the root of the volume in the MFT data.");
            }

            _log.Debug("Searching for the target path node: {TargetPath}", targetPath);
            var resultNode = FindNodeByPath(volumeRoot, targetPath);
            if (resultNode == null)
            {
                _log.Error("Could not locate target path '{TargetPath}' in the constructed MFT tree.", targetPath);
                throw new DirectoryNotFoundException($"Could not locate path '{targetPath}' in the constructed MFT tree.");
            }

            _log.Information("Found target path node: {NodeName}", resultNode.Name);
            return resultNode;
        }

        private long BuildFullPathsAndSizes(FileSystemNode node, string parentPath)
        {
            node.FullPath = Path.Combine(parentPath, node.Name);
            if (node.IsDirectory)
            {
                long totalSize = 0;
                // Create a copy of children collection to avoid modification issues if any
                var children = new List<FileSystemNode>(node.Children);
                node.Children.Clear(); // Clear original to re-add sorted
                foreach (var child in children)
                {
                    totalSize += BuildFullPathsAndSizes(child, node.FullPath);
                }
                node.Size = totalSize;
                // Optional: Sort children alphabetically
                node.Children.AddRange(children.OrderBy(c => c.Name));
            }
            return node.Size;
        }

        private FileSystemNode? FindNodeByPath(FileSystemNode startNode, string path)
        {
            if (startNode.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                return startNode;
            }

            // More efficient search: only descend if the child path is a prefix of the target path.
            foreach (var child in startNode.Children)
            {
                if (path.StartsWith(child.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    var found = FindNodeByPath(child, path);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private SafeFileHandle GetVolumeHandle(string driveLetter)
        {
            var path = $@"\\.\{driveLetter}:";
            _log.Debug("Calling CreateFile for path: {Path}", path);
            return MftApi.CreateFile(path, FileAccess.Read, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, MftApi.FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
        }

        private static class MftApi
        {
            public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
            public const int ERROR_NO_MORE_FILES = 18;
            public const int ERROR_HANDLE_EOF = 38;
            public const uint FSCTL_ENUM_USN_DATA = 0x000900b3;
            public const int USN_RECORD_COMMON_HEADER_SIZE = 8;

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern SafeFileHandle CreateFile(string lpFileName, [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess, [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode, IntPtr lpSecurityAttributes, [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

            [StructLayout(LayoutKind.Sequential)]
            public struct MFT_ENUM_DATA_V0 { public long StartFileReferenceNumber; public long LowUsn; public long HighUsn; }

            [StructLayout(LayoutKind.Sequential)]
            public struct USN_RECORD_V2
            {
                public uint RecordLength;
                public ushort MajorVersion;
                public ushort MinorVersion;
                public long FileReferenceNumber;
                public long ParentFileReferenceNumber;
                public long Usn;
                public long TimeStamp;
                public uint Reason;
                public uint SourceInfo;
                public uint SecurityId;
                public uint FileAttributes;
                public ushort FileNameLength;
                public ushort FileNameOffset;
                // The FileName string is variable length and follows this structure in memory.
                // It is read manually instead of being part of the struct declaration.
            }
        }
    }
}
