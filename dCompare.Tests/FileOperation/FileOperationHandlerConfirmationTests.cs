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
    /// Covers the confirmation gate added to <see cref="FileOperationHandler"/>: the Options | Confirmation
    /// flags (Copy / Move / CopyOverwrite / MoveOverwrite / DeleteFile) are read once per batch, on the calling
    /// thread, before any QueueTransferMessage or IFileManager call, and the approved overwrite value — not a
    /// hard-coded true — is what reaches CopyAsync/MoveAsync.
    /// </summary>
    public class FileOperationHandlerConfirmationTests : IDisposable
    {
        private readonly string _testRoot;
        private readonly string _sourceRoot;
        private readonly string _targetRoot;
        private readonly Mock<IFileManager> _fileManager = new();
        private readonly List<QueueTransferMessage> _queued = new();
        private readonly List<CompleteTransferMessage> _completed = new();
        private Mock<IUserInteractionService> _interaction = new();

        public FileOperationHandlerConfirmationTests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), "FileOperationHandlerConfirmationTests", Path.GetRandomFileName());
            _sourceRoot = Path.Combine(_testRoot, "source");
            _targetRoot = Path.Combine(_testRoot, "target");
            Directory.CreateDirectory(_sourceRoot);
            Directory.CreateDirectory(_targetRoot);

            // A successful, non-cancelled manager by default: the handler turns a zero SuccessCount into a
            // MessageBox, which would block the test run.
            SetupManager(Success());
            // Filter to this test's own sources: the messenger is a process-wide singleton and other test
            // classes (e.g. FileOperationHandlerCancellationTests) run in parallel and send their own transfers.
            WeakReferenceMessenger.Default.Register<QueueTransferMessage>(this, (r, m) =>
            {
                if (m.Transfer.Source.StartsWith(_testRoot, StringComparison.OrdinalIgnoreCase)) _queued.Add(m);
            });
            WeakReferenceMessenger.Default.Register<CompleteTransferMessage>(this, (r, m) =>
            {
                if (m.Transfer.Source.StartsWith(_testRoot, StringComparison.OrdinalIgnoreCase)) _completed.Add(m);
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
                // Files can still be held by a worker thread that just finished.
            }
        }

        private static OperationResult Success() => new(1, 0, new List<string>(), TimeSpan.Zero);

        private void SetupManager(OperationResult result)
        {
            _fileManager.Setup(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
            _fileManager.Setup(m => m.MoveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
            _fileManager.Setup(m => m.DeleteAsync(It.IsAny<string>(),
                    It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        /// <summary>
        /// Builds the handler around the confirmation flags under test. The interaction mock is exposed through
        /// <see cref="_interaction"/> so each test can assert how often it was asked.
        /// </summary>
        private FileOperationHandler HandlerWith(ConfirmationOptions confirmation, bool confirmResult,
            bool withConfig = true, bool withInteraction = true)
        {
            var config = new AppConfiguration { Options = new dCompare.Configuration.Options { Confirmation = confirmation } };
            var configService = new Mock<IConfigurationService>();
            configService.Setup(c => c.LoadConfigurationAsync()).ReturnsAsync(config);

            _interaction = new Mock<IUserInteractionService>();
            _interaction.Setup(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(confirmResult);

            return new FileOperationHandler(_fileManager.Object,
                withConfig ? configService.Object : null,
                withInteraction ? _interaction.Object : null);
        }

        // Source files have to exist: ExecuteTransferAsync reads new FileInfo(source).Length while queueing, and
        // an exception there would land in the handler's MessageBox error path.
        private string CreateSourceFile(string relativePath, string content = "content")
        {
            string source = Path.Combine(_sourceRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            File.WriteAllText(source, content);
            return source;
        }

        private static TreeNode TransferNode(string relativePath, string sourceFullPath, bool onLeft = true)
        {
            var node = new FileSystemNode
            {
                Name = Path.GetFileName(sourceFullPath),
                FullPath = sourceFullPath,
                IsDirectory = false
            };
            return new TreeNode
            {
                Name = node.Name,
                RelativePath = relativePath,
                IsDirectory = false,
                LeftNode = onLeft ? node : null,
                RightNode = onLeft ? null : node
            };
        }

        private static async Task RunWithTimeout(Func<Task> action)
        {
            // A prompt that escapes to MessageBox (empty selection, error path) would hang the whole run, so the
            // handler calls are bounded.
            Task work = Task.Run(action);
            await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.True(work.IsCompleted, "FileOperationHandler did not return within 30s — it probably reached a dialog.");
            await work;
        }

        private void VerifyCopyCalled(string source, string target, bool overwrite, Func<Times> times) =>
            _fileManager.Verify(m => m.CopyAsync(source, target, overwrite,
                It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()), times);

        private void VerifyMoveCalled(string source, string target, bool overwrite, Func<Times> times) =>
            _fileManager.Verify(m => m.MoveAsync(source, target, overwrite,
                It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()), times);

        private void VerifyNoTransfer()
        {
            _fileManager.Verify(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
            _fileManager.Verify(m => m.MoveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
            _fileManager.Verify(m => m.DeleteAsync(It.IsAny<string>(),
                It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---- Copy -----------------------------------------------------------------------------------------

        [Fact]
        public async Task CopyToRight_ConfirmCopy_Declined_StartsNoTransferAndQueuesNothing()
        {
            string source = CreateSourceFile("a\\b.txt");
            var handler = HandlerWith(new ConfirmationOptions { Copy = true }, confirmResult: false);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("a\\b.txt", source) }, _targetRoot));

            VerifyNoTransfer();
            Assert.Empty(_queued);
            Assert.Empty(_completed);
            _interaction.Verify(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _interaction.Verify(u => u.ShowInformation(string.Format(dCompare.Properties.Resources.StatusOperationCancelled,
                    dCompare.Properties.Resources.VerbCopy, 1, _targetRoot),
                dCompare.Properties.Resources.ConfirmOperation), Times.Once);
        }

        [Fact]
        public async Task CopyToRight_ConfirmCopy_Accepted_PromptsOnceForTheWholeBatch()
        {
            string first = CreateSourceFile("a\\one.txt");
            string second = CreateSourceFile("a\\two.txt");
            var handler = HandlerWith(new ConfirmationOptions { Copy = true }, confirmResult: true);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode>
            {
                TransferNode("a\\one.txt", first),
                TransferNode("a\\two.txt", second)
            }, _targetRoot));

            _interaction.Verify(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            VerifyCopyCalled(first, Path.Combine(_targetRoot, "a", "one.txt"), false, Times.Once);
            VerifyCopyCalled(second, Path.Combine(_targetRoot, "a", "two.txt"), false, Times.Once);
            Assert.Equal(2, _queued.Count);
            Assert.Equal(2, _completed.Count);
        }

        [Fact]
        public async Task CopyToRight_NothingAtDestination_PassesOverwriteFalse()
        {
            string source = CreateSourceFile("solo.txt");
            var handler = HandlerWith(new ConfirmationOptions { Copy = true, CopyOverwrite = true }, confirmResult: true);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("solo.txt", source) }, _targetRoot));

            VerifyCopyCalled(source, Path.Combine(_targetRoot, "solo.txt"), false, Times.Once);
            VerifyCopyCalled(source, Path.Combine(_targetRoot, "solo.txt"), true, Times.Never);
        }

        [Fact]
        public async Task CopyToRight_ExistingDestinationFile_PassesOverwriteTrue()
        {
            string source = CreateSourceFile("solo.txt", "new");
            string destination = Path.Combine(_targetRoot, "solo.txt");
            File.WriteAllText(destination, "old");
            var handler = HandlerWith(new ConfirmationOptions { Copy = true }, confirmResult: true);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("solo.txt", source) }, _targetRoot));

            VerifyCopyCalled(source, destination, true, Times.Once);
            VerifyCopyCalled(source, destination, false, Times.Never);
        }

        [Fact]
        public async Task CopyToRight_CopyOverwriteOnly_WithExistingDestination_Prompts()
        {
            string source = CreateSourceFile("over.txt", "new");
            string destination = Path.Combine(_targetRoot, "over.txt");
            File.WriteAllText(destination, "old");
            var handler = HandlerWith(new ConfirmationOptions { Copy = false, CopyOverwrite = true }, confirmResult: true);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("over.txt", source) }, _targetRoot));

            _interaction.Verify(u => u.ShowConfirmation(It.Is<string>(m =>
                    m.Contains(dCompare.Properties.Resources.ConfirmTransferOverwriteWarning)
                    && m.Contains(_targetRoot, StringComparison.OrdinalIgnoreCase)),
                dCompare.Properties.Resources.ConfirmOperation), Times.Once);
            VerifyCopyCalled(source, destination, true, Times.Once);
        }

        [Fact]
        public async Task CopyToRight_CopyOverwriteOnly_NothingAtDestination_DoesNotPrompt()
        {
            string source = CreateSourceFile("fresh.txt");
            var handler = HandlerWith(new ConfirmationOptions { Copy = false, CopyOverwrite = true }, confirmResult: false);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("fresh.txt", source) }, _targetRoot));

            _interaction.Verify(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            VerifyCopyCalled(source, Path.Combine(_targetRoot, "fresh.txt"), false, Times.Once);
        }

        [Fact]
        public async Task CopyToRight_AllConfirmationFlagsOff_DoesNotPrompt()
        {
            string source = CreateSourceFile("quiet.txt");
            string destination = Path.Combine(_targetRoot, "quiet.txt");
            File.WriteAllText(destination, "old");
            var handler = HandlerWith(new ConfirmationOptions
            {
                Copy = false,
                Move = false,
                CopyOverwrite = false,
                MoveOverwrite = false,
                DeleteFile = false
            }, confirmResult: false);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("quiet.txt", source) }, _targetRoot));

            _interaction.Verify(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            VerifyCopyCalled(source, destination, true, Times.Once);
        }

        [Fact]
        public async Task CopyToRight_ConfirmationMessage_NamesVerbCountAndTarget()
        {
            string source = CreateSourceFile("named.txt");
            var handler = HandlerWith(new ConfirmationOptions { Copy = true }, confirmResult: true);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("named.txt", source) }, _targetRoot));

            string expected = string.Format(dCompare.Properties.Resources.ConfirmTransferMessage,
                dCompare.Properties.Resources.VerbCopy, 1, _targetRoot);
            _interaction.Verify(u => u.ShowConfirmation(expected, dCompare.Properties.Resources.ConfirmOperation), Times.Once);
        }

        // ---- Move -----------------------------------------------------------------------------------------

        [Fact]
        public async Task MoveToLeft_MoveOverwriteOnly_WithExistingDestinationFolder_PromptsAndPassesOverwriteTrue()
        {
            string source = CreateSourceFile("mover.txt");
            // The destination is a directory here: the overwrite probe has to catch Directory.Exists too.
            string destination = Path.Combine(_targetRoot, "mover.txt");
            Directory.CreateDirectory(destination);
            var handler = HandlerWith(new ConfirmationOptions { Move = false, MoveOverwrite = true }, confirmResult: true);

            await RunWithTimeout(() => handler.MoveToLeft(new List<TreeNode> { TransferNode("mover.txt", source, onLeft: false) }, _targetRoot));

            _interaction.Verify(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            VerifyMoveCalled(source, destination, true, Times.Once);
        }

        [Fact]
        public async Task MoveToRight_ConfirmMove_Declined_StartsNoTransferAndQueuesNothing()
        {
            string source = CreateSourceFile("declined.txt");
            var handler = HandlerWith(new ConfirmationOptions { Move = true }, confirmResult: false);

            await RunWithTimeout(() => handler.MoveToRight(new List<TreeNode> { TransferNode("declined.txt", source) }, _targetRoot));

            VerifyNoTransfer();
            Assert.Empty(_queued);
            _interaction.Verify(u => u.ShowInformation(string.Format(dCompare.Properties.Resources.StatusOperationCancelled,
                    dCompare.Properties.Resources.VerbMove, 1, _targetRoot),
                dCompare.Properties.Resources.ConfirmOperation), Times.Once);
        }

        [Fact]
        public async Task MoveToRight_ConfirmMove_Accepted_PassesOverwriteFalseWhenDestinationIsFree()
        {
            string source = CreateSourceFile("moved.txt");
            var handler = HandlerWith(new ConfirmationOptions { Move = true }, confirmResult: true);

            await RunWithTimeout(() => handler.MoveToRight(new List<TreeNode> { TransferNode("moved.txt", source) }, _targetRoot));

            VerifyMoveCalled(source, Path.Combine(_targetRoot, "moved.txt"), false, Times.Once);
            Assert.Single(_completed);
            Assert.True(_completed[0].Success);
        }

        // ---- Delete ---------------------------------------------------------------------------------------

        private static List<(string Path, SourceType Type)> DeleteItems(params string[] paths) =>
            paths.Select(p => (p, SourceType.Left)).ToList();

        [Fact]
        public async Task DeleteAsync_ConfirmDelete_Declined_DeletesNothingAndQueuesNothing()
        {
            string victim = CreateSourceFile("victim.txt");
            var handler = HandlerWith(new ConfirmationOptions { DeleteFile = true }, confirmResult: false);

            await RunWithTimeout(() => handler.DeleteAsync(DeleteItems(victim)));

            _fileManager.Verify(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<IProgress<OperationProgress>?>(),
                It.IsAny<CancellationToken>()), Times.Never);
            Assert.Empty(_queued);
            Assert.True(File.Exists(victim));
            _interaction.Verify(u => u.ShowInformation(string.Format(dCompare.Properties.Resources.StatusDeleteCancelled, 1),
                dCompare.Properties.Resources.ConfirmOperation), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ConfirmDelete_PromptsOnceForTheBatchAndNamesTheTargets()
        {
            string first = CreateSourceFile("d1.txt");
            string second = CreateSourceFile("sub\\d2.txt");
            var handler = HandlerWith(new ConfirmationOptions { DeleteFile = true }, confirmResult: true);

            await RunWithTimeout(() => handler.DeleteAsync(DeleteItems(first, second)));

            _interaction.Verify(u => u.ShowConfirmation(It.Is<string>(m =>
                    m.Contains(string.Format(dCompare.Properties.Resources.ConfirmDeleteItems, 2))
                    && m.Contains(first, StringComparison.OrdinalIgnoreCase)
                    && m.Contains(second, StringComparison.OrdinalIgnoreCase)),
                dCompare.Properties.Resources.ConfirmOperation), Times.Once);
            _fileManager.Verify(m => m.DeleteAsync(first, It.IsAny<IProgress<OperationProgress>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
            _fileManager.Verify(m => m.DeleteAsync(second, It.IsAny<IProgress<OperationProgress>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ConfirmDeleteOff_DeletesWithoutPrompting()
        {
            string victim = CreateSourceFile("nodelay.txt");
            var handler = HandlerWith(new ConfirmationOptions { DeleteFile = false }, confirmResult: false);

            await RunWithTimeout(() => handler.DeleteAsync(DeleteItems(victim)));

            _interaction.Verify(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _fileManager.Verify(m => m.DeleteAsync(victim, It.IsAny<IProgress<OperationProgress>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(_queued);
        }

        [Fact]
        public async Task DeleteAsync_NullOptionsSection_FallsBackToDefaultsAndPrompts()
        {
            string victim = CreateSourceFile("defaults.txt");
            var config = new AppConfiguration { Options = null! };
            var configService = new Mock<IConfigurationService>();
            configService.Setup(c => c.LoadConfigurationAsync()).ReturnsAsync(config);
            _interaction = new Mock<IUserInteractionService>();
            _interaction.Setup(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
            var handler = new FileOperationHandler(_fileManager.Object, configService.Object, _interaction.Object);

            await RunWithTimeout(() => handler.DeleteAsync(DeleteItems(victim)));

            // DeleteFile defaults to true, so a config whose Options section is missing still asks.
            _interaction.Verify(u => u.ShowConfirmation(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _fileManager.Verify(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<IProgress<OperationProgress>?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---- Handler built without the optional services ----------------------------------------------------

        [Fact]
        public async Task CopyToRight_ConfigWithoutInteractionService_TransfersWithoutPrompting()
        {
            string source = CreateSourceFile("noservice.txt");
            var handler = HandlerWith(new ConfirmationOptions { Copy = true, CopyOverwrite = true },
                confirmResult: false, withInteraction: false);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("noservice.txt", source) }, _targetRoot));

            VerifyCopyCalled(source, Path.Combine(_targetRoot, "noservice.txt"), false, Times.Once);
        }

        [Fact]
        public async Task Handler_WithoutConfigOrInteraction_RunsEveryOperation()
        {
            // The parameterless-defaulted ctor shape the pre-confirmation tests used: nothing can prompt, so
            // nothing may throw.
            string source = CreateSourceFile("legacy.txt");
            string victim = CreateSourceFile("legacy-victim.txt");
            var handler = new FileOperationHandler(_fileManager.Object);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("legacy.txt", source) }, _targetRoot));
            await RunWithTimeout(() => handler.MoveToLeft(new List<TreeNode> { TransferNode("legacy.txt", source, onLeft: false) }, _targetRoot));
            await RunWithTimeout(() => handler.DeleteAsync(DeleteItems(victim)));

            VerifyCopyCalled(source, Path.Combine(_targetRoot, "legacy.txt"), false, Times.Once);
            _fileManager.Verify(m => m.DeleteAsync(victim, It.IsAny<IProgress<OperationProgress>?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ---- Cancellation is reported, not booked as success ------------------------------------------------

        [Fact]
        public async Task CopyToRight_ManagerCancels_ReportsItemAsCancelledNotSuccess()
        {
            string source = CreateSourceFile("cancel-me.txt");
            _fileManager.Setup(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<OperationProgress>?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            var handler = HandlerWith(new ConfirmationOptions { Copy = true }, confirmResult: true);

            await RunWithTimeout(() => handler.CopyToRight(new List<TreeNode> { TransferNode("cancel-me.txt", source) }, _targetRoot));

            Assert.Single(_queued);
            CompleteTransferMessage message = Assert.Single(_completed);
            Assert.True(message.Cancelled);
            Assert.False(message.Success);
            Assert.Equal(dCompare.Properties.Resources.TransferCanceled, message.Message);
        }

        [Fact]
        public async Task DeleteAsync_ManagerCancels_ReportsItemAsCancelledNotSuccess()
        {
            string victim = CreateSourceFile("cancel-delete.txt");
            _fileManager.Setup(m => m.DeleteAsync(It.IsAny<string>(), It.IsAny<IProgress<OperationProgress>?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            var handler = HandlerWith(new ConfirmationOptions { DeleteFile = false }, confirmResult: true);

            await RunWithTimeout(() => handler.DeleteAsync(DeleteItems(victim)));

            CompleteTransferMessage message = Assert.Single(_completed);
            Assert.True(message.Cancelled);
            Assert.False(message.Success);
            Assert.Equal(dCompare.Properties.Resources.TransferCanceled, message.Message);
        }
    }
}
