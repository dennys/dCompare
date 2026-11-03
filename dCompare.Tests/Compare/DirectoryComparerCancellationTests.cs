using dCompare.Compare;
using dCompare.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.Compare
{
    public class DirectoryComparerCancellationTests : IDisposable
    {
        private readonly string _tempDir;

        public DirectoryComparerCancellationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dCompareTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void ComputeXxHash64_CancelledToken_ThrowsOperationCanceled()
        {
            using var stream = new MemoryStream(new byte[1 << 20]); // 1MB
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() =>
                new HashProvider().ComputeXxHash64(stream, 1.0, cts.Token));
        }

        [Fact]
        public void ComputeMD5_CancelledToken_ThrowsOperationCanceled()
        {
            using var stream = new MemoryStream(new byte[1 << 20]); // 1MB
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() =>
                new HashProvider().ComputeMD5(stream, 1.0, cts.Token));
        }

        [Fact]
        public async Task CompareAsync_PreCancelledToken_ThrowsInsteadOfReturningResult()
        {
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);
            File.WriteAllBytes(Path.Combine(leftPath, "a.txt"), new byte[1024]);
            File.WriteAllBytes(Path.Combine(rightPath, "a.txt"), new byte[1024]);

            var comparer = new DirectoryComparer(new AppConfiguration(), new FileSystem(), new HashProvider());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // A cancelled compare must throw, never return a (half-built) result list.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                comparer.CompareAsync(leftPath, rightPath, cts.Token, null, ComparisonAlgorithm.FullContents));
        }

        [Fact]
        public async Task CompareAsync_CancelDuringStreamingComparison_ThrowsOperationCanceled()
        {
            // Deterministic: the injected file system cancels the compare the first time the streaming
            // loop reads a chunk, so the very next chunk-boundary check must throw. The old version raced
            // a CancelAfter(10ms) against a warm-cache 128MB compare and failed ~1 run in 3.
            string leftPath = Path.Combine(_tempDir, "left");
            string rightPath = Path.Combine(_tempDir, "right");
            Directory.CreateDirectory(leftPath);
            Directory.CreateDirectory(rightPath);

            var content = new byte[4 * 1024 * 1024]; // > 8KB chunks, so the loop runs many times
            new Random(5).NextBytes(content);
            string leftFile = Path.Combine(leftPath, "big.bin");
            string rightFile = Path.Combine(rightPath, "big.bin");
            File.WriteAllBytes(leftFile, content);
            File.WriteAllBytes(rightFile, content);
            var stamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(leftFile, stamp);
            File.SetLastWriteTimeUtc(rightFile, stamp);

            using var cts = new CancellationTokenSource();
            var fileSystem = new CancelOnFirstReadFileSystem(new FileSystem(), cts);
            var comparer = new DirectoryComparer(new AppConfiguration(), fileSystem, new HashProvider());

            var task = comparer.CompareAsync(leftPath, rightPath, cts.Token, null, ComparisonAlgorithm.FullContents);

            // Assert: it aborted because of the cancellation, it did not run to completion.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(fileSystem.SawRead, "the streaming comparison never started");
        }

        // Delegates everything to the inner file system but requests cancellation on the first Read of the
        // file contents, which puts the cancel exactly inside the streaming comparison loop.
        private sealed class CancelOnFirstReadFileSystem : IFileSystem
        {
            private readonly IFileSystem _inner;
            private readonly CancellationTokenSource _cts;
            private int _readCount;

            public CancelOnFirstReadFileSystem(IFileSystem inner, CancellationTokenSource cts)
            {
                _inner = inner;
                _cts = cts;
            }

            public bool SawRead => Volatile.Read(ref _readCount) > 0;

            public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
            public bool FileExists(string path) => _inner.FileExists(path);
            public IEnumerable<string> GetDirectories(string path) => _inner.GetDirectories(path);
            public IEnumerable<string> GetFiles(string path) => _inner.GetFiles(path);
            public DateTime GetLastWriteTime(string path) => _inner.GetLastWriteTime(path);
            public FileInfo GetFileInfo(string path) => _inner.GetFileInfo(path);

            public Stream OpenRead(string path) => new CancellingStream(_inner.OpenRead(path), this);

            internal void OnRead()
            {
                if (Interlocked.Increment(ref _readCount) == 1)
                {
                    _cts.Cancel();
                }
            }

            private sealed class CancellingStream : Stream
            {
                private readonly Stream _inner;
                private readonly CancelOnFirstReadFileSystem _owner;

                public CancellingStream(Stream inner, CancelOnFirstReadFileSystem owner)
                {
                    _inner = inner;
                    _owner = owner;
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    int read = _inner.Read(buffer, offset, count);
                    _owner.OnRead();
                    return read;
                }

                public override bool CanRead => _inner.CanRead;
                public override bool CanSeek => _inner.CanSeek;
                public override bool CanWrite => false;
                public override long Length => _inner.Length;
                public override long Position { get => _inner.Position; set => _inner.Position = value; }
                public override void Flush() => _inner.Flush();
                public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
                public override void SetLength(long value) => throw new NotSupportedException();
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

                protected override void Dispose(bool disposing)
                {
                    if (disposing) _inner.Dispose();
                    base.Dispose(disposing);
                }
            }
        }
    }
}
