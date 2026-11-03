using dCompare.FileOperation;
using dCompare.FileOperation.Strategies;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.FileOperation
{
    /// <summary>
    /// Covers the optional CancellationToken added to IFileManager.CopyAsync/MoveAsync/DeleteAsync: an already
    /// cancelled token must fail the operation, never let it run or report it as a success.
    /// </summary>
    public class FileManagerCancellationTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _sourceDir;
        private readonly string _destDir;

        public FileManagerCancellationTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "FileManagerCancellationTests", Path.GetRandomFileName());
            _sourceDir = Path.Combine(_testRoot, "source");
            _destDir = Path.Combine(_testRoot, "destination");
            Directory.CreateDirectory(_sourceDir);
            Directory.CreateDirectory(_destDir);
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
                // Nothing to do: the batch never started.
            }
        }

        private static CancellationToken Cancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            return cts.Token;
        }

        private string CreateSourceFile(string name, string content = "content")
        {
            string path = Path.Combine(_sourceDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        private FileManager NewManager() => new(new FileStreamStrategy());

        [Fact]
        public async Task CopyAsync_AlreadyCancelledToken_CancelsAndCopiesNothing()
        {
            string source = CreateSourceFile("file.txt");
            string destination = Path.Combine(_destDir, "file.txt");

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => NewManager().CopyAsync(source, destination, true, null, Cancelled()));

            Assert.False(File.Exists(destination));
            Assert.True(File.Exists(source));
        }

        [Fact]
        public async Task MoveAsync_AlreadyCancelledToken_CancelsAndMovesNothing()
        {
            string source = CreateSourceFile("move-me.txt");
            string destination = Path.Combine(_destDir, "move-me.txt");

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => NewManager().MoveAsync(source, destination, true, null, Cancelled()));

            Assert.False(File.Exists(destination));
            Assert.True(File.Exists(source));
        }

        [Fact]
        public async Task DeleteAsync_AlreadyCancelledToken_CancelsAndKeepsTheFile()
        {
            string victim = CreateSourceFile("victim.txt");

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => NewManager().DeleteAsync(victim, null, Cancelled()));

            Assert.True(File.Exists(victim));
        }

        [Fact]
        public async Task DeleteMultipleAsync_AlreadyCancelledToken_CancelsAndKeepsEveryFile()
        {
            string first = CreateSourceFile("keep1.txt");
            string second = CreateSourceFile("keep2.txt");

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => NewManager().DeleteMultipleAsync(new System.Collections.Generic.List<string> { first, second }, null, Cancelled()));

            Assert.True(File.Exists(first));
            Assert.True(File.Exists(second));
        }
    }
}
