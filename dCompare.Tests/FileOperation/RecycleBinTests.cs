using dCompare.FileOperation;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace dCompare.Tests.FileOperation
{
    /// <summary>
    /// Covers RecycleBin.TryDelete (internal, reached by reflection so the production assembly needs no
    /// InternalsVisibleTo): false means "nothing was deleted" and the caller's permanent-delete fallback has to
    /// run, true means the path is gone. Nothing here asserts anything about the Recycle Bin UI.
    /// </summary>
    public class RecycleBinTests : IDisposable
    {
        private static readonly Type RecycleBinType =
            typeof(FileManager).Assembly.GetType("dCompare.FileOperation.RecycleBin")
            ?? throw new InvalidOperationException("dCompare.FileOperation.RecycleBin is gone.");

        private static readonly MethodInfo TryDeleteMethod =
            RecycleBinType.GetMethod("TryDelete", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("RecycleBin.TryDelete(string, bool) is gone.");

        private readonly string _testRoot;

        public RecycleBinTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "RecycleBinTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, true);
                }
            }
            catch (IOException)
            {
                // A shell delete can briefly outlive the test.
            }
        }

        private static bool TryDelete(string path, bool isDirectory) =>
            (bool)TryDeleteMethod.Invoke(null, new object[] { path, isDirectory })!;

        [Fact]
        public void TryDelete_MissingFile_ReturnsFalse()
        {
            string missing = Path.Combine(_testRoot, "nope.txt");

            Assert.False(TryDelete(missing, isDirectory: false));
            Assert.False(File.Exists(missing));
        }

        [Fact]
        public void TryDelete_MissingDirectory_ReturnsFalse()
        {
            string missing = Path.Combine(_testRoot, "nope");

            Assert.False(TryDelete(missing, isDirectory: true));
            Assert.False(Directory.Exists(missing));
        }

        [Fact]
        public void TryDelete_ExistingFile_ReturnsTrueAndPathIsGone()
        {
            string file = Path.Combine(_testRoot, "recycle-me.txt");
            File.WriteAllText(file, "content");

            bool recycled = TryDelete(file, isDirectory: false);

            AssertDeleteReportedWhatItDid(recycled, () => File.Exists(file));
        }

        [Fact]
        public void TryDelete_ExistingDirectoryTree_ReturnsTrueAndPathIsGone()
        {
            string dir = Path.Combine(_testRoot, "tree");
            Directory.CreateDirectory(Path.Combine(dir, "sub"));
            File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
            File.WriteAllText(Path.Combine(dir, "sub", "b.txt"), "b");

            bool recycled = TryDelete(dir, isDirectory: true);

            AssertDeleteReportedWhatItDid(recycled, () => Directory.Exists(dir));
        }

        // The documented contract, and the deterministic one: a volume without a Recycle Bin makes the shell
        // throw, TryDelete reports false and leaves the item alone so FileManager's permanent fallback deletes
        // it. Where the shell does work, the item has to be gone.
        private static void AssertDeleteReportedWhatItDid(bool recycled, Func<bool> stillThere)
        {
            if (!recycled)
            {
                Assert.True(stillThere(), "TryDelete returned false but deleted the item anyway; the caller's " +
                    "permanent-delete fallback would report a phantom failure.");
                return;
            }

            Assert.False(stillThere(), "TryDelete returned true but the item is still on disk.");
        }
    }
}
