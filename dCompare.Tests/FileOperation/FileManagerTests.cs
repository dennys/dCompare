using dCompare.Compare;
using dCompare.FileOperation;
using dCompare.FileOperation.Models;
using dCompare.FileOperation.Strategies;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Xunit;

namespace dCompare.Tests.FileOperation
{
    public class FileManagerTests
    {
        private readonly Mock<IFileOperationStrategy> _strategyMock;
        private readonly FileManager _fileManager;

        public FileManagerTests()
        {
            _strategyMock = new Mock<IFileOperationStrategy>();
            _fileManager = new FileManager(_strategyMock.Object);
        }

        [Fact]
        //public async Task CopyAsync_SingleFile_CallsStrategyAndReportsProgress()
        //{
        //    // Arrange
        //    string source = "source.txt";
        //    string target = "target.txt";
        //    bool overwrite = true;
        //    var progressMock = new Mock<IProgress<OperationProgress>>();
        //    var cts = new CancellationTokenSource();
        //    _strategyMock.Setup(s => s.CopyFileAsync(source, target, overwrite, It.IsAny<CopyProgressCallback>(), cts.Token))
        //        .Returns(Task.CompletedTask);

        //    // Act
        //    var result = await _fileManager.CopyAsync(source, target, overwrite, progressMock.Object, cts.Token);

        //    // Assert
        //    _strategyMock.Verify(s => s.CopyFileAsync(source, target, overwrite, It.IsAny<CopyProgressCallback>(), cts.Token), Times.Once());
        //    progressMock.Verify(p => p.Report(It.IsAny<OperationProgress>()), Times.AtLeastOnce());
        //    Assert.Equal(1, result.SuccessCount);
        //    Assert.Empty(result.FailedItems);
        //}

        //[Fact]
        public async Task CopyMultipleAsync_MultipleFiles_ProcessesAllItems()
        {
            // Arrange
            var items = new List<(string source, string target, SourceType type)>
            {
                ("file1.txt", "dest1.txt", SourceType.Left),
                ("file2.txt", "dest2.txt", SourceType.Left)
            };
            var progressMock = new Mock<IProgress<OperationProgress>>();
            var cts = new CancellationTokenSource();
            _strategyMock.Setup(s => s.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), false, null, cts.Token))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _fileManager.CopyMultipleAsync(items, false, progressMock.Object, cts.Token);

            // Assert
            _strategyMock.Verify(s => s.CopyFileAsync(It.IsAny<string>(), It.IsAny<string>(), false, null, cts.Token), Times.Exactly(2));
            progressMock.Verify(p => p.Report(It.IsAny<OperationProgress>()), Times.AtLeastOnce());
            Assert.Equal(2, result.SuccessCount);
            Assert.Empty(result.FailedItems);
        }

        [Fact]
        public async Task MoveAsync_SingleFile_CallsStrategyMoveFile()
        {
            // Arrange
            string source = "source.txt";
            string target = "target.txt";
            bool overwrite = false;
            var cts = new CancellationTokenSource();
            _strategyMock.Setup(s => s.MoveFile(source, target, overwrite));

            // Act
            var result = await _fileManager.MoveAsync(source, target, overwrite, null, cts.Token);

            // Assert
            _strategyMock.Verify(s => s.MoveFile(source, target, overwrite), Times.Once());
            Assert.Equal(1, result.SuccessCount);
            Assert.Empty(result.FailedItems);
        }

        [Fact]
        public async Task DeleteAsync_SingleFile_CallsStrategyDeleteFile()
        {
            // Arrange
            string path = "file.txt";
            var cts = new CancellationTokenSource();
            _strategyMock.Setup(s => s.DeleteFile(path));

            // Act
            var result = await _fileManager.DeleteAsync(path, null, cts.Token);

            // Assert
            _strategyMock.Verify(s => s.DeleteFile(path), Times.Once());
            Assert.Equal(1, result.SuccessCount);
            Assert.Empty(result.FailedItems);
        }

        [Fact]
        public async Task DeleteMultipleAsync_MultiplePaths_ProcessesAllItems()
        {
            // Arrange
            var paths = new List<string> { "file1.txt", "file2.txt" };
            var cts = new CancellationTokenSource();
            _strategyMock.Setup(s => s.DeleteFile(It.IsAny<string>()));

            // Act
            var result = await _fileManager.DeleteMultipleAsync(paths, null, cts.Token);

            // Assert
            _strategyMock.Verify(s => s.DeleteFile(It.IsAny<string>()), Times.Exactly(2));
            Assert.Equal(2, result.SuccessCount);
            Assert.Empty(result.FailedItems);
        }

        //[Fact]
        //public async Task CopyAsync_CancellationRequested_ThrowsOperationCanceledException()
        //{
        //    // Arrange
        //    string source = "source.txt";
        //    string target = "target.txt";
        //    var cts = new CancellationTokenSource();
        //    _strategyMock.Setup(s => s.CopyFileAsync(source, target, false, It.IsAny<CopyProgressCallback>(), cts.Token))
        //        .Callback(() => cts.Cancel())
        //        .Throws(new OperationCanceledException());

        //    // Act & Assert
        //    await Assert.ThrowsAsync<OperationCanceledException>(() =>
        //        _fileManager.CopyAsync(source, target, false, null, cts.Token));
        //}

        //[Fact]
        //public async Task CopyAsync_Paused_DelaysUntilResumed()
        //{
        //    // Arrange
        //    string source = "source.txt";
        //    string target = "target.txt";
        //    var cts = new CancellationTokenSource();
        //    _fileManager.Pause();
        //    var task = _fileManager.CopyAsync(source, target, false, null, cts.Token);
        //    await Task.Delay(100); // Give some time to ensure pause is effective

        //    // Act
        //    _fileManager.Resume();
        //    _strategyMock.Setup(s => s.CopyFileAsync(source, target, false, It.IsAny<CopyProgressCallback>(), cts.Token))
        //        .Returns(Task.CompletedTask);
        //    var result = await task;

        //    // Assert
        //    _strategyMock.Verify(s => s.CopyFileAsync(source, target, false, It.IsAny<CopyProgressCallback>(), cts.Token), Times.Once());
        //    Assert.Equal(1, result.SuccessCount);
        //}

        //[Fact]
        //public async Task CopyAsync_FileThrowsException_RecordsFailure()
        //{
        //    // Arrange
        //    string source = "source.txt";
        //    string target = "target.txt";
        //    var cts = new CancellationTokenSource();
        //    var exception = new IOException("Copy failed");
        //    _strategyMock.Setup(s => s.CopyFileAsync(source, target, false, It.IsAny<CopyProgressCallback>(), cts.Token))
        //        .Throws(exception);

        //    // Act
        //    var result = await _fileManager.CopyAsync(source, target, false, null, cts.Token);

        //    // Assert
        //    Assert.Equal(0, result.SuccessCount);
        //    Assert.Single(result.FailedItems);
        //    Assert.Contains(exception.Message, result.FailedItems[0]);
        //}
    }
}