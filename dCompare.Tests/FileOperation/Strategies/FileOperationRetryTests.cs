using System.ComponentModel;
using System.IO;
using dCompare.FileOperation.Strategies;

namespace dCompare.Tests.FileOperation.Strategies
{
    public class FileOperationRetryTests
    {
        [Fact]
        public void Run_SucceedsAfterTemporarySharingViolation_Retries()
        {
            var calls = 0;
            FileOperationRetry.Run("test", () =>
            {
                calls++;
                if (calls < 3) throw new Win32Exception(32); // ERROR_SHARING_VIOLATION
            });

            Assert.Equal(3, calls);
        }

        [Fact]
        public void Run_SucceedsAfterLockViolation_Retries()
        {
            var calls = 0;
            FileOperationRetry.Run("test", () =>
            {
                calls++;
                if (calls < 2) throw new Win32Exception(33); // ERROR_LOCK_VIOLATION
            });

            Assert.Equal(2, calls);
        }

        [Fact]
        public void Run_NonTemporaryError_DoesNotRetry()
        {
            var calls = 0;
            var ex = Record.Exception(() => FileOperationRetry.Run("test", () =>
            {
                calls++;
                throw new Win32Exception(2); // ERROR_FILE_NOT_FOUND
            }));

            Assert.NotNull(ex);
            Assert.IsType<Win32Exception>(ex);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Run_GenericException_DoesNotRetry()
        {
            var calls = 0;
            var ex = Record.Exception(() => FileOperationRetry.Run("test", () =>
            {
                calls++;
                throw new InvalidOperationException("boom");
            }));

            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void Run_ExhaustsRetries_ThenThrows()
        {
            var calls = 0;
            var ex = Record.Exception(() => FileOperationRetry.Run("test", () =>
            {
                calls++;
                throw new Win32Exception(32);
            }));

            Assert.NotNull(ex);
            Assert.IsType<Win32Exception>(ex);
            Assert.Equal(3, calls); // 1 + 2 retries
        }

        #region MoveWithOverwrite / TryDelete

        [Fact]
        public void MoveWithOverwrite_ReadOnlyDestination_IsReplaced()
        {
            using var scratch = new Scratch();
            string staged = scratch.Create("staged.txt", "new");
            string destination = scratch.Create("dest.txt", "old");
            File.SetAttributes(destination, FileAttributes.ReadOnly);

            // MoveFileEx refuses to replace a read-only target, which used to make "copy over" a read-only
            // file a hard failure in the shipped (Kernel32) path.
            FileOperationRetry.MoveWithOverwrite(staged, destination, overwrite: true);

            Assert.Equal("new", File.ReadAllText(destination));
            Assert.False(File.Exists(staged));
        }

        [Fact]
        public void MoveWithOverwrite_MoveFails_DestinationKeepsItsReadOnlyAttribute()
        {
            using var scratch = new Scratch();
            string staged = scratch.Create("staged.txt", "new");
            string destination = scratch.Create("dest.txt", "old");
            File.SetAttributes(destination, FileAttributes.ReadOnly);

            // A destination held by Explorer, antivirus or OneDrive is what actually fails in the field.
            using (File.Open(destination, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsAny<Exception>(() => FileOperationRetry.MoveWithOverwrite(staged, destination, overwrite: true));
            }

            Assert.Equal("old", File.ReadAllText(destination));
            Assert.True((File.GetAttributes(destination) & FileAttributes.ReadOnly) != 0,
                "The move did not happen, so the user's file must not be left writable.");
            Assert.True(File.Exists(staged), "A move that never ran must not consume the staged file.");
        }

        [Fact]
        public void TryDelete_ReadOnlyStagingFile_DeletesIt()
        {
            using var scratch = new Scratch();
            string staged = scratch.Create("dest.txt.dCompare.tmp", "half written");
            File.SetAttributes(staged, FileAttributes.ReadOnly);

            // CopyFileEx stamps the staging file with the source's attributes and File.Delete refuses a
            // read-only file: the leftover of a failed copy then makes every later copy into the same folder
            // die on ERROR_FILE_EXISTS, because the staging name is fixed.
            FileOperationRetry.TryDelete(staged);

            Assert.False(File.Exists(staged));
        }

        // Recursive delete cannot remove read-only files, which several of these tests create on purpose.
        private sealed class Scratch : IDisposable
        {
            private readonly string _root =
                Path.Combine(Path.GetTempPath(), "FileOperationRetryTests_" + Guid.NewGuid().ToString()[..8]);

            public string Create(string name, string content)
            {
                Directory.CreateDirectory(_root);
                string path = Path.Combine(_root, name);
                File.WriteAllText(path, content);
                return path;
            }

            public void Dispose()
            {
                if (!Directory.Exists(_root)) return;
                foreach (string file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                Directory.Delete(_root, true);
            }
        }

        #endregion
    }
}
