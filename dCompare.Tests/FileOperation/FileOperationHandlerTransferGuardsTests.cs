using CommunityToolkit.Mvvm.Messaging;
using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.DirTreeSystem.Models;
using dCompare.FileOperation;
using dCompare.FileOperation.Messages;
using dCompare.FileOperation.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace dCompare.Tests.FileOperation
{
    /// <summary>
    /// Two guards on the transfer path: a large selection must not open one task per file, and rows read from
    /// inside an archive must never reach the file operations at all.
    /// </summary>
    public class FileOperationHandlerTransferGuardsTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _sourceRoot;
        private readonly string _targetRoot;
        private readonly Mock<IFileManager> _fileManager = new();

        public FileOperationHandlerTransferGuardsTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "FileOperationGuardTests", Path.GetRandomFileName());
            _sourceRoot = Path.Combine(_testRoot, "source");
            _targetRoot = Path.Combine(_testRoot, "target");
            Directory.CreateDirectory(_sourceRoot);
            Directory.CreateDirectory(_targetRoot);

            // The messenger is a process-wide singleton; other test classes run in parallel and send their own.
            WeakReferenceMessenger.Default.Register<CompleteTransferMessage>(this, (r, m) => { });
        }

        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            try
            {
                if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
            }
            catch (IOException)
            {
            }
        }

        private FileOperationHandler Handler()
        {
            var configService = new Mock<IConfigurationService>();
            configService.Setup(c => c.LoadConfigurationAsync())
                .ReturnsAsync(new AppConfiguration { Options = new dCompare.Configuration.Options() });
            var interaction = new Mock<IUserInteractionService>();
            interaction.Setup(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            return new FileOperationHandler(_fileManager.Object, configService.Object, interaction.Object);
        }

        private string CreateSourceFile(string name)
        {
            string path = Path.Combine(_sourceRoot, name);
            File.WriteAllText(path, "content");
            return path;
        }

        // CopyToRight takes its source from the LEFT side (toLeft selects which side receives the copy), so the
        // rows below carry a LeftNode; a row with only a RightNode selects nothing and the handler warns instead.
        private static TreeNode Node(string relativePath, string fullPath, bool isArchiveEntry = false)
        {
            var node = new FileSystemNode
            {
                Name = Path.GetFileName(fullPath),
                FullPath = fullPath,
                IsDirectory = false,
                IsArchiveEntry = isArchiveEntry
            };
            return new TreeNode
            {
                Name = node.Name,
                RelativePath = relativePath,
                IsDirectory = false,
                LeftNode = node
            };
        }

        [Fact]
        public async Task CopyManyItems_NeverRunsMoreThanTheTransferGate()
        {
            const int itemCount = 40;
            int inFlight = 0;
            int peak = 0;

            _fileManager.Setup(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    int now = Interlocked.Increment(ref inFlight);
                    if (now > peak) Volatile.Write(ref peak, now);
                    await Task.Delay(25);
                    Interlocked.Decrement(ref inFlight);
                    return new OperationResult(1, 0, new List<string>(), TimeSpan.Zero);
                });

            var nodes = new List<TreeNode>();
            for (int i = 0; i < itemCount; i++)
            {
                nodes.Add(Node($"file{i}.txt", CreateSourceFile($"file{i}.txt")));
            }

            Task work = Task.Run(() => Handler().CopyToRight(nodes, _targetRoot));
            await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(60)));
            Assert.True(work.IsCompleted, "CopyToRight did not finish — the transfer gate may be deadlocking.");
            await work;

            _fileManager.Verify(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()), Times.Exactly(itemCount));
            Assert.True(peak > 1, $"the batch ran fully serially (peak {peak}); parallel transfers were lost");
            Assert.True(peak <= 4, $"peak concurrent transfers was {peak}; the selection opened one task per file");
        }

        [Fact]
        public void ArchiveEntryRows_AreNeverOfferedForTransferOrDeletion()
        {
            string insideZip = @"C:\archived\notes.zip\docs\note.txt";
            TreeNode archiveRow = Node("docs/note.txt", insideZip, isArchiveEntry: true);
            TreeNode realRow = Node("real.txt", CreateSourceFile("real.txt"));

            Assert.True(FileOperationHandler.ContainsArchiveEntry(new[] { realRow, archiveRow }));

            var handler = Handler();
            List<(string Source, string TargetPath, SourceType Type)> transfers =
                handler.GetItemsForTransfer(new List<TreeNode> { realRow, archiveRow }, toLeft: false, _targetRoot);
            List<(string Path, SourceType Type)> deletions =
                handler.GetItemsForDeletion(new List<TreeNode> { realRow, archiveRow }, isLeft: true);

            Assert.Single(transfers);
            Assert.Single(deletions);
            Assert.Equal(realRow.LeftNode!.FullPath, transfers[0].Source);
            Assert.Equal(realRow.LeftNode.FullPath, deletions[0].Path);
        }
    }
}
