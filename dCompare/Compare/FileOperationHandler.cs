using dCompare.FileOperation;
using Microsoft.VisualBasic;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace dCompare.Compare
{
    public class FileOperationHandler
    {
        private readonly FileManager _fileManager;
        private readonly TreeNodeManager _treeNodeManager;
        private FileOperationWindow _fileOperationWindow;

        // Dictionary to track active jobs and their completion sources.
        private readonly Dictionary<string, TaskCompletionSource<bool>> _activeJobs = new Dictionary<string, TaskCompletionSource<bool>>();

        public FileOperationHandler(FileManager fileManager, TreeNodeManager treeNodeManager)
        {
            _fileManager = fileManager;
            _treeNodeManager = treeNodeManager;
        }

        public List<(string Source, string TargetPath, SourceType Type)> GetItemsForTransfer(List<TreeNode> selectedItems, bool toLeft, string targetRoot, string sourceRoot)
        {
            List<(string Source, string TargetPath, SourceType Type)> items = new();
            foreach (var node in selectedItems)
            {
                string sourcePath = toLeft ? node.RightNode?.FullPath : node.LeftNode?.FullPath;
                if (string.IsNullOrEmpty(sourcePath)) continue;

                string relativePath = node.RelativePath ?? string.Empty;
                string destDir = Path.Combine(targetRoot, Path.GetDirectoryName(relativePath) ?? string.Empty);
                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(destDir, fileName);

                items.Add((sourcePath, destPath, toLeft ? SourceType.Right : SourceType.Left));
            }
            return items;
        }

        public List<(string Path, SourceType Type)> GetItemsForDeletion(List<TreeNode> selectedItems, bool isLeft)
        {
            List<(string Path, SourceType Type)> paths = new();
            foreach (var node in selectedItems)
            {
                string path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add((path, isLeft ? SourceType.Left : SourceType.Right));
                }
            }
            return paths;
        }

        public async Task CopyToLeft(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, leftRoot, rightRoot);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行同步");
                return;
            }

            try
            {
                OpenFileOperations();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    // Get the list of job IDs for the operations we are starting.
                    var jobIds = _fileOperationWindow.CopyMultiple(items, true);

                    // Create a list of tasks to wait for.
                    var tasks = new List<Task>();
                    foreach (var jobId in jobIds)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        _activeJobs[jobId] = tcs;
                        tasks.Add(tcs.Task);
                    }

                    // Asynchronously wait for all the file operation tasks to complete.
                    await Task.WhenAll(tasks);

                    //_fileOperationWindow.CopyMultiple(items, true);
                    //foreach (var (source, target, type) in items)
                    //{
                    //    //bool isDirectory = Directory.Exists(source);
                    //    //var relativePath = target.Substring(leftRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                    //    //var node = _treeNodeManager.FindOrCreateNode(relativePath, isDirectory);
                    //    //_treeNodeManager.UpdateNodeAfterOperation(node, target, source, isDirectory);
                    //}
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task CopyToRight(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, rightRoot, leftRoot);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行同步");
                return;
            }

            try
            {
                OpenFileOperations();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    // Get the list of job IDs for the operations we are starting.
                    var jobIds = _fileOperationWindow.CopyMultiple(items, true);

                    // Create a list of tasks to wait for.
                    var tasks = new List<Task>();
                    foreach (var jobId in jobIds)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        _activeJobs[jobId] = tcs;
                        tasks.Add(tcs.Task);
                    }

                    // Asynchronously wait for all the file operation tasks to complete.
                    await Task.WhenAll(tasks);

                    //_fileOperationWindow.CopyMultiple(items, true);
                    //foreach (var (source, target, type) in items)
                    //{
                    //    //bool isDirectory = Directory.Exists(source);
                    //    //var relativePath = target.Substring(rightRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                    //    //var node = _treeNodeManager.FindOrCreateNode(relativePath, isDirectory);
                    //    //_treeNodeManager.UpdateNodeAfterOperation(node, source, target, isDirectory);
                    //}
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task MoveToLeft(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: true, leftRoot, rightRoot);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行移動");
                return;
            }

            try
            {
                OpenFileOperations();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    // Get the list of job IDs for the operations we are starting.
                    var jobIds = _fileOperationWindow.MoveMultiple(items, true);

                    // Create a list of tasks to wait for.
                    var tasks = new List<Task>();
                    foreach (var jobId in jobIds)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        _activeJobs[jobId] = tcs;
                        tasks.Add(tcs.Task);
                    }

                    // Asynchronously wait for all the file operation tasks to complete.
                    await Task.WhenAll(tasks);

                    //_fileOperationWindow.MoveMultiple(items, true);
                    //foreach (var (source, target, type) in items)
                    {
                        //bool isDirectory = Directory.Exists(source);
                        //var relativePath = target.Substring(leftRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                        //var node = _treeNodeManager.FindOrCreateNode(relativePath, isDirectory);
                        //_treeNodeManager.UpdateNodeAfterOperation(node, target, null, isDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error moving items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task MoveToRight(List<TreeNode> selectedItems, string leftRoot, string rightRoot)
        {
            var items = GetItemsForTransfer(selectedItems, toLeft: false, rightRoot, leftRoot);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行移動");
                return;
            }

            try
            {
                OpenFileOperations();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    // Get the list of job IDs for the operations we are starting.
                    var jobIds = _fileOperationWindow.MoveMultiple(items, true);

                    // Create a list of tasks to wait for.
                    var tasks = new List<Task>();
                    foreach (var jobId in jobIds)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        _activeJobs[jobId] = tcs;
                        tasks.Add(tcs.Task);
                    }

                    // Asynchronously wait for all the file operation tasks to complete.
                    await Task.WhenAll(tasks);

                    //_fileOperationWindow.MoveMultiple(items, true);
                    //foreach (var (source, target, type) in items)
                    //{
                    //    //bool isDirectory = Directory.Exists(source);
                    //    //var relativePath = target.Substring(rightRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                    //    //var node = _treeNodeManager.FindOrCreateNode(relativePath, isDirectory);
                    //    //_treeNodeManager.UpdateNodeAfterOperation(node, null, target, isDirectory);
                    //}
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error moving items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Rename(TreeNode selectedItem, bool isLeft, string leftRoot, string rightRoot)
        {
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }

            string sourcePath = isLeft ? selectedItem.LeftNode?.FullPath : selectedItem.RightNode?.FullPath;
            if (string.IsNullOrEmpty(sourcePath))
            {
                MessageBox.Show("No valid file or directory path selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string currentName = Path.GetFileName(sourcePath);
            string newName = Interaction.InputBox("Enter new name:", "Rename File/Directory", currentName);

            if (string.IsNullOrEmpty(newName) || newName == currentName)
            {
                return;
            }

            try
            {
                string parentDir = Path.GetDirectoryName(sourcePath);
                string newPath = Path.Combine(parentDir, newName);

                if (selectedItem.IsDirectory)
                {
                    Directory.Move(sourcePath, newPath);
                }
                else
                {
                    File.Move(sourcePath, newPath);
                }

                string relativePath = newPath.Substring(isLeft ? leftRoot.Length : rightRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                selectedItem.RelativePath = relativePath;
                //_treeNodeManager.UpdateNodeAfterOperation(selectedItem, isLeft ? newPath : null, isLeft ? null : newPath, selectedItem.IsDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task Delete(ObservableCollection<TreeNode> _treeNodes, List<TreeNode> selectedItems, bool isLeft)
        {
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("請選擇至少一個項目");
                return;
            }

            var items = GetItemsForDeletion(selectedItems, isLeft);
            if (items.Count == 0)
            {
                MessageBox.Show("No valid file or directory paths selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string itemList = string.Join("\n", items.Select(i => Path.GetFileName(i.Path)));
            string message = $"Are you sure you want to delete the following {items.Count} item(s)?\n\n{itemList}";
            var result = MessageBox.Show(
                message,
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                OpenFileOperations();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    _fileManager.DeleteMultipleAsync(items.Select(i => i.Path).ToList()).GetAwaiter().GetResult();
                    foreach (var (path, type) in items)
                    {
                        var node = selectedItems.FirstOrDefault(n => (isLeft ? n.LeftNode?.FullPath : n.RightNode?.FullPath) == path);
                        if (node != null)
                        {
                            //_treeNodeManager.UpdateNodeAfterOperation(node, isLeft ? null : node.LeftNode?.FullPath, isLeft ? node.RightNode?.FullPath : null, node.IsDirectory);
                            if (node.LeftNode == null && node.RightNode == null)
                            {
                                _treeNodes.Remove(node);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileOperations()
        {
            if (_fileOperationWindow == null || !_fileOperationWindow.IsLoaded)
            {
                _fileOperationWindow = new FileOperationWindow();
                _fileOperationWindow.Closed += (s, args) => _fileOperationWindow = null;
                _fileOperationWindow.OnTransferStatusChanged += FileOperation_OnTransferStatusChanged;
                _fileOperationWindow.Show();
            }
            else
            {
                _fileOperationWindow.Activate();
            }
        }
        private void FileOperation_OnTransferStatusChanged(string jobId, bool success, string message)
        {
            string a = $"Job {jobId}: {message}";
            Log.Information($"complete copy or move or delete: {a}");
            //MessageBox.Show(a);

            // Find the TaskCompletionSource for the completed job.
            if (_activeJobs.TryGetValue(jobId, out var tcs))
            {
                if (success)
                {
                    // Signal that the task completed successfully.
                    tcs.TrySetResult(true);
                }
                else
                {
                    // Signal that the task failed.
                    tcs.TrySetException(new Exception(message));
                }
                // Remove the completed job from tracking.
                _activeJobs.Remove(jobId);
            }
        }
    }
}