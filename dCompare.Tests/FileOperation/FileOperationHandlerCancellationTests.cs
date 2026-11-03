using CommunityToolkit.Mvvm.Messaging;
using dCompare.Compare.Models;
using dCompare.DirTreeSystem.Models;
using dCompare.FileOperation;
using dCompare.FileOperation.Messages;
using dCompare.FileOperation.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.FileOperation
{
    // The operation panel's per-row "cancel" used to call the manager's global Cancel(), so right-clicking
    // one row aborted the entire queue. Each transfer now carries its own token (linked to the batch token).
    public class FileOperationHandlerCancellationTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _targetRoot;
        private readonly Mock<IFileManager> _managerMock;
        private readonly FileOperationHandler _handler;
        private readonly object _gate = new();
        private readonly List<FileTransfer> _queued = new();
        private readonly List<CompleteTransferMessage> _completed = new();

        public FileOperationHandlerCancellationTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "FileOperationHandlerCancellationTests", Path.GetRandomFileName());
            _targetRoot = Path.Combine(_testRoot, "target");
            Directory.CreateDirectory(_targetRoot);

            _managerMock = new Mock<IFileManager>();
            _handler = new FileOperationHandler(_managerMock.Object);
            // 'this' stays alive for the whole test, so the weak references resolve. The filter is needed
            // because the messenger is a process-wide singleton and other test classes run in parallel.
            // The lists are touched by worker threads (messenger delivery) and the test thread, hence the lock.
            WeakReferenceMessenger.Default.Register<QueueTransferMessage>(this, (_, m) =>
            {
                if (m.Transfer.Source.StartsWith(_testRoot, StringComparison.OrdinalIgnoreCase))
                {
                    lock (_gate) { _queued.Add(m.Transfer); }
                }
            });
            WeakReferenceMessenger.Default.Register<CompleteTransferMessage>(this, (_, m) =>
            {
                if (m.Transfer.Source.StartsWith(_testRoot, StringComparison.OrdinalIgnoreCase))
                {
                    lock (_gate) { _completed.Add(m); }
                }
            });
        }

        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, true);
                }
            }
            catch (IOException)
            {
            }
        }

        private static TreeNode MakeNode(string name, string relativePath, string fullLeftPath) => new()
        {
            Name = name,
            RelativePath = relativePath,
            IsDirectory = false,
            LeftNode = new FileSystemNode { Name = name, FullPath = fullLeftPath, IsDirectory = false }
        };

        private async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!condition() && stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(20);
            }
            Assert.True(condition(), "condition was not met within the timeout");
        }

        private FileTransfer QueuedSingle(string source)
        {
            lock (_gate)
            {
                return _queued.Single(t => t.Source == source);
            }
        }

        private CompleteTransferMessage CompletedSingle(FileTransfer transfer)
        {
            lock (_gate)
            {
                if (_completed.Count == 0)
                {
                    throw new InvalidOperationException("no completions received; queued=" + _queued.Count);
                }
                return _completed.Single(m => m.Transfer == transfer);
            }
        }

        private IReadOnlyList<CompleteTransferMessage> CompletedSnapshot()
        {
            lock (_gate)
            {
                return _completed.ToList();
            }
        }

        [Fact]
        public async Task CancelOneTransfer_StopsOnlyThatTransfer()
        {
            // Arrange: two transfers; the "slow" one hangs in the manager until its token is cancelled,
            // the other one completes normally.
            string slowSource = Path.Combine(_testRoot, "slow.txt");
            string fastSource = Path.Combine(_testRoot, "fast.txt");
            File.WriteAllText(slowSource, "slow");
            File.WriteAllText(fastSource, "fast");

            _managerMock
                .Setup(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IProgress<OperationProgress>>(), It.IsAny<CancellationToken>()))
                .Returns(async (string source, string destination, bool overwrite, IProgress<OperationProgress>? progress, CancellationToken token) =>
                {
                    if (source == slowSource)
                    {
                        await Task.Delay(Timeout.Infinite, token); // hangs until this transfer's token is cancelled
                    }
                    return new OperationResult(1, 0, new List<string>(), TimeSpan.Zero);
                });

            var nodes = new List<TreeNode>
            {
                MakeNode("slow.txt", "slow.txt", slowSource),
                MakeNode("fast.txt", "fast.txt", fastSource)
            };

            // Act
            Task batch = _handler.CopyToRight(nodes, _targetRoot);

            await WaitUntilAsync(() =>
            {
                lock (_gate) { return _queued.Count == 2; }
            });
            FileTransfer slowTransfer = QueuedSingle(slowSource);
            FileTransfer fastTransfer = QueuedSingle(fastSource);

            Assert.NotNull(slowTransfer.Cancellation); // every transfer carries its own token now
            slowTransfer.CancelTransfer();

            await batch;

            // Assert: the cancelled one is reported Cancelled, the other one finished successfully.
            CompleteTransferMessage slowResult = CompletedSingle(slowTransfer);
            CompleteTransferMessage fastResult = CompletedSingle(fastTransfer);
            Assert.True(slowResult.Cancelled);
            Assert.False(slowResult.Success);
            Assert.True(fastResult.Success);
        }

        // Global manager Cancel (the big button) must still stop every transfer.
        [Fact]
        public async Task GlobalCancel_StopsAllTransfers()
        {
            string slowSource = Path.Combine(_testRoot, "slow.txt");
            string slowSource2 = Path.Combine(_testRoot, "slow2.txt");
            File.WriteAllText(slowSource, "slow");
            File.WriteAllText(slowSource2, "slow");

            _managerMock
                .Setup(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IProgress<OperationProgress>>(), It.IsAny<CancellationToken>()))
                .Returns(async (string source, string destination, bool overwrite, IProgress<OperationProgress>? progress, CancellationToken token) =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return new OperationResult(1, 0, new List<string>(), TimeSpan.Zero);
                });

            var nodes = new List<TreeNode>
            {
                MakeNode("slow.txt", "slow.txt", slowSource),
                MakeNode("slow2.txt", "slow2.txt", slowSource2)
            };

            Task batch = _handler.CopyToRight(nodes, _targetRoot);
            await WaitUntilAsync(() =>
            {
                lock (_gate) { return _queued.Count == 2; }
            });

            _handler.Cancel(); // the global stop
            await batch;

            var results = CompletedSnapshot();
            Assert.Equal(2, results.Count);
            Assert.All(results, m => Assert.True(m.Cancelled));
        }
    }
}
