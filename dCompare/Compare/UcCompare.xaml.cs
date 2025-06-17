using dCompare.Diff;
using dCompare.FileOperation;
using dCompare.FileOperation.Strategies;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace dCompare.Compare
{
    public enum SourceType
    {
        Left,
        Right
    }

    public partial class UcCompare : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<TreeNode> _treeNodes = new();
        private DirectoryComparer _comparer = new DirectoryComparer();
        private ListSortDirection _lastSortDirection;
        private GridViewColumnHeader _lastHeaderClicked;
        private Predicate<object> _currentFilter = null;
        private List<DirectoryComparer.ComparisonItem>? _cachedComparisonResults;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isComparing;
        private double _comparisonProgress;

        private FileOperationWindow _fileOperationWindow;
        private readonly FileManager _fileManager = new FileManager(new Kernel32FileCopyStrategy());

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsComparing
        {
            get => _isComparing;
            set
            {
                _isComparing = value;
                OnPropertyChanged(nameof(IsComparing));
                UpdateTabHeader();
            }
        }

        public double ComparisonProgress
        {
            get => _comparisonProgress;
            set
            {
                _comparisonProgress = value;
                OnPropertyChanged(nameof(ComparisonProgress));
            }
        }

        public UcCompare()
        {
            InitializeComponent();
            ResultListView.ItemsSource = _treeNodes;
            ResultListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeaderClickedHandler));
            DataContext = this;
        }

        private void UpdateTabHeader()
        {
            string baseHeader = LeftPathBox.Text.Trim() + " - " + RightPathBox.Text.Trim();
            if (LogicalTreeHelperExtensions.FindParent<TabItem>(this) is TabItem tabItem)
            {
                tabItem.Header = IsComparing ? $"(Comparing...) {baseHeader}" : baseHeader;
            }
        }

        public void ChangeTabHeader(string newHeader)
        {
            TabItem tabItem = LogicalTreeHelperExtensions.FindParent<TabItem>(this);
            if (tabItem != null && !IsComparing)
            {
                tabItem.Header = newHeader;
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog { Title = "Select Folder" };
            if (folderDialog.ShowDialog() == true)
            {
                var textBox = (sender as Button)?.Name == "BrowseLeftButton" ? LeftPathBox : RightPathBox;
                textBox.Text = folderDialog.FolderName;
            }
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            RefreshComparison();
        }

        private async void RefreshComparison()
        {
            if (IsComparing) { MessageBox.Show("Comparison is already in progress."); return; }

            _treeNodes.Clear();
            _cachedComparisonResults = null;
            _cancellationTokenSource = new CancellationTokenSource();

            string leftDir = LeftPathBox.Text.Trim();
            string rightDir = RightPathBox.Text.Trim();

            if (!Directory.Exists(leftDir) || !Directory.Exists(rightDir))
            {
                MessageBox.Show("Please enter valid source and target folder paths.");
                return;
            }

            IsComparing = true;
            ComparisonProgress = 0;
            try
            {
                List<DirectoryComparer.ComparisonItem>? comparisonResults = await Task.Run(() =>
                    _comparer.Compare(leftDir, rightDir, _cancellationTokenSource.Token,
                        progress => Dispatcher.Invoke(() => ComparisonProgress = progress * 100)),
                    _cancellationTokenSource.Token);

                _cachedComparisonResults = comparisonResults;
                List<TreeNode>? flatNodes = CreateFlatTreeStructure(comparisonResults, leftDir, rightDir, maxDepth: 0);

                foreach (TreeNode? node in flatNodes)
                {
                    node.IsExpanded = false;
                    _treeNodes.Add(node);
                }

                ChangeTabHeader(LeftPathBox.Text + " - " + RightPathBox.Text);
            }
            catch (OperationCanceledException)
            {
                _treeNodes.Clear();
                MessageBox.Show("Comparison was canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during comparison: {ex.Message}");
            }
            finally
            {
                IsComparing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelCompare_Click(object sender, RoutedEventArgs e) => _cancellationTokenSource?.Cancel();

        private List<TreeNode> CreateFlatTreeStructure(List<DirectoryComparer.ComparisonItem> items, string leftRoot, string rightRoot, int maxDepth = int.MaxValue)
        {
            List<TreeNode> flatNodes = new List<TreeNode>();

            foreach (DirectoryComparer.ComparisonItem? item in items)
            {
                string? relativePath = GetRelativePath(item, leftRoot, rightRoot);
                if (string.IsNullOrEmpty(relativePath)) continue;

                int depth = relativePath.Count(c => c == Path.DirectorySeparatorChar);
                if (depth > maxDepth) continue;

                TreeNode node = new TreeNode
                {
                    Name = item.Left?.Name ?? item.Right?.Name ?? Path.GetFileName(relativePath),
                    RelativePath = relativePath,
                    IsDirectory = item.Left?.IsDirectory ?? item.Right?.IsDirectory ?? false,
                    Depth = depth
                };

                SetNodeDetails(node, item, leftRoot, rightRoot);
                flatNodes.Add(node);
            }

            return flatNodes;
        }

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                string sortBy = headerClicked.Column.Header as string;
                ListSortDirection direction = _lastHeaderClicked == headerClicked && _lastSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

                ICollectionView? view = CollectionViewSource.GetDefaultView(_treeNodes);
                view.SortDescriptions.Clear();

                string propertyName = sortBy switch
                {
                    "Left Name" => "LeftDisplayName",
                    "Left Size" => "LeftSize",
                    "Left Date" => "LeftDate",
                    "Right Name" => "RightDisplayName",
                    "Right Size" => "RightSize",
                    "Right Date" => "RightDate",
                    "Status" => "StatusSymbol",
                    _ => "LeftDisplayName"
                };

                view.SortDescriptions.Add(new SortDescription(propertyName, direction));
                _lastHeaderClicked = headerClicked;
                _lastSortDirection = direction;
            }
        }

        private async void ResultListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject obj = (DependencyObject)e.OriginalSource;
            while (obj != null && obj is not ListViewItem)
            {
                obj = VisualTreeHelper.GetParent(obj);
            }

            if (obj is ListViewItem item && item.Content is TreeNode clickedItem)
            {
                if (clickedItem.IsDirectory)
                {
                    if (clickedItem.IsExpanded)
                    {
                        clickedItem.IsExpanded = false;
                        RemoveChildren(clickedItem);
                    }
                    else
                    {
                        clickedItem.IsExpanded = true;
                        LoadChildren(clickedItem);
                    }
                }
                else
                {
                    string leftPath = clickedItem.LeftNode?.FullPath;
                    string rightPath = clickedItem.RightNode?.FullPath;

                    if (string.IsNullOrEmpty(leftPath) && string.IsNullOrEmpty(rightPath))
                    {
                        MessageBox.Show("No valid file paths to compare.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var comparisonHandler = new FileComparisonHandler();
                    FileType fileType = comparisonHandler.DetermineFileType(leftPath ?? rightPath);

                    try
                    {
                        leftPath = leftPath ?? CreateTemporaryEmptyFile();
                        rightPath = rightPath ?? CreateTemporaryEmptyFile();

                        if (fileType == FileType.Text)
                        {
                            var comparisonResult = await comparisonHandler.CompareFilesAsync(leftPath, rightPath, fileType);
                            OpenFileComparisonTab(leftPath, rightPath, comparisonResult, fileType);
                        }
                        else if (fileType == FileType.Image)
                        {
                            OpenFileComparisonTab(leftPath, rightPath, null, fileType);
                        }
                        else
                        {
                            MessageBox.Show("Unsupported file type for comparison.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error comparing files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private string CreateTemporaryEmptyFile()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"dCompare_Empty_{Guid.NewGuid()}.txt");
            File.WriteAllText(tempPath, string.Empty);
            return tempPath;
        }

        private void OpenFileComparisonTab(string leftPath, string rightPath, object comparisonResult, FileType fileType)
        {
            TabItem parentTab = LogicalTreeHelperExtensions.FindParent<TabItem>(this);
            TabControl tabControl = LogicalTreeHelperExtensions.FindParent<TabControl>(parentTab);
            if (tabControl == null)
            {
                MessageBox.Show("Cannot find parent TabControl.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string tabHeader = $"Compare: {Path.GetFileName(leftPath)} - {Path.GetFileName(rightPath)}";
            TabItem newTab = new TabItem { Header = tabHeader };

            if (fileType == FileType.Text)
            {
                var textViewer = new UcFileCompare();
                textViewer.SetComparisonResult(leftPath, rightPath, comparisonResult);
                newTab.Content = textViewer;
            }
            else if (fileType == FileType.Image)
            {
                var imageViewer = new UcImageDiffViewer();
                imageViewer.SetImagePaths(leftPath, rightPath);
                newTab.Content = imageViewer;
            }
            else
            {
                MessageBox.Show("Unsupported file type for comparison.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            tabControl.Items.Add(newTab);
            tabControl.SelectedItem = newTab;
        }

        private void LoadChildren(TreeNode parentNode)
        {
            if (_cachedComparisonResults == null) return;

            string leftDir = LeftPathBox.Text.Trim();
            string rightDir = RightPathBox.Text.Trim();

            List<DirectoryComparer.ComparisonItem> childItems = _cachedComparisonResults
                .Where(item =>
                {
                    string? relativePath = GetRelativePath(item, leftDir, rightDir);
                    return !string.IsNullOrEmpty(relativePath) &&
                           relativePath.StartsWith(parentNode.RelativePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                           relativePath.Count(c => c == Path.DirectorySeparatorChar) == parentNode.Depth + 1;
                })
                .ToList();

            List<TreeNode> childNodes = CreateFlatTreeStructure(childItems, leftDir, rightDir, parentNode.Depth + 1);
            int insertIndex = _treeNodes.IndexOf(parentNode) + 1;

            foreach (TreeNode child in childNodes)
            {
                child.IsExpanded = false;
                _treeNodes.Insert(insertIndex++, child);
            }
        }

        private void RemoveChildren(TreeNode parentNode)
        {
            var children = _treeNodes
                .Where(n => n.RelativePath.StartsWith(parentNode.RelativePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var child in children)
            {
                _treeNodes.Remove(child);
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            if (_cachedComparisonResults == null) return;

            string leftDir = LeftPathBox.Text.Trim();
            string rightDir = RightPathBox.Text.Trim();

            var rootNodes = _treeNodes.Where(n => n.Depth == 0).ToList();
            _treeNodes.Clear();

            List<TreeNode> allNodes = CreateFlatTreeStructure(_cachedComparisonResults, leftDir, rightDir);

            foreach (TreeNode node in allNodes)
            {
                node.IsExpanded = node.IsDirectory;
                _treeNodes.Add(node);
            }

            ApplyFilter();
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            var rootNodes = _treeNodes.Where(n => n.Depth == 0).ToList();
            _treeNodes.Clear();
            foreach (TreeNode node in rootNodes)
            {
                node.IsExpanded = false;
                _treeNodes.Add(node);
            }
            ApplyFilter();
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e) => SetFilter(null);
        private void ShowDifferent_Click(object sender, RoutedEventArgs e) => SetFilter(node => node.Status == ComparisonStatus.Modified || node.Status == ComparisonStatus.Conflict);
        private void ShowIdentical_Click(object sender, RoutedEventArgs e) => SetFilter(node => node.Status == ComparisonStatus.Identical);
        private void ShowLeftUnique_Click(object sender, RoutedEventArgs e) => SetFilter(node => node.Status == ComparisonStatus.LeftOnly);
        private void ShowRightUnique_Click(object sender, RoutedEventArgs e) => SetFilter(node => node.Status == ComparisonStatus.RightOnly);

        private void SetFilter(Predicate<TreeNode> filter)
        {
            _currentFilter = obj => obj is TreeNode node && (filter == null || filter(node));
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            CollectionViewSource.GetDefaultView(_treeNodes).Refresh();
        }

        private List<(string Source, string TargetPath, SourceType Type)> GetSelectedItemsForTransfer(bool toLeft)
        {
            List<(string Source, string TargetPath, SourceType Type)> items = new();
            string targetRoot = toLeft ? LeftPathBox.Text.Trim() : RightPathBox.Text.Trim();
            string sourceRoot = toLeft ? RightPathBox.Text.Trim() : LeftPathBox.Text.Trim();

            foreach (TreeNode node in ResultListView.SelectedItems.Cast<TreeNode>())
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

        private List<string> GetSelectedItemsForDeletion(bool isLeft)
        {
            List<string> paths = new();
            foreach (TreeNode node in ResultListView.SelectedItems.Cast<TreeNode>())
            {
                string path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
            return paths;
        }

        private void ContextMenu_CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            var items = GetSelectedItemsForTransfer(toLeft: true);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行同步");
                return;
            }

            try
            {
                OpenFileOperations_Click();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    _fileOperationWindow.CopyMultiple(items, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RefreshComparison();
        }

        private void ContextMenu_CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            var items = GetSelectedItemsForTransfer(toLeft: false);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行同步");
                return;
            }

            try
            {
                OpenFileOperations_Click();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    _fileOperationWindow.CopyMultiple(items, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RefreshComparison();
        }

        private void ContextMenu_MoveToLeft_Click(object sender, RoutedEventArgs e)
        {
            var items = GetSelectedItemsForTransfer(toLeft: true);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行移動");
                return;
            }

            try
            {
                OpenFileOperations_Click();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    _fileOperationWindow.MoveMultiple(items, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error moving items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RefreshComparison();
        }

        private void ContextMenu_MoveToRight_Click(object sender, RoutedEventArgs e)
        {
            var items = GetSelectedItemsForTransfer(toLeft: false);
            if (items.Count == 0)
            {
                MessageBox.Show("請選擇至少一個文件或目錄進行移動");
                return;
            }

            try
            {
                OpenFileOperations_Click();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    _fileOperationWindow.MoveMultiple(items, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error moving items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            RefreshComparison();
        }

        private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultListView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }

            bool isLeft = (sender as MenuItem)?.Parent is ContextMenu contextMenu &&
                          contextMenu == FindResource("LeftContextMenu");
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

                RefreshComparison();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("請選擇至少一個項目");
                return;
            }

            bool isLeft = (sender as MenuItem)?.Parent is ContextMenu contextMenu &&
                          contextMenu == FindResource("LeftContextMenu");
            var paths = GetSelectedItemsForDeletion(isLeft);

            if (paths.Count == 0)
            {
                MessageBox.Show("No valid file or directory paths selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string itemList = string.Join("\n", paths.Select(p => Path.GetFileName(p)));
            string message = $"Are you sure you want to delete the following {paths.Count} item(s)?\n\n{itemList}";
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
                OpenFileOperations_Click();
                if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
                {
                    await _fileManager.DeleteMultipleAsync(paths);
                }
                RefreshComparison();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting items: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ContextMenu_CompareContents_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultListView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }

            if (selectedItem.IsDirectory)
            {
                MessageBox.Show("Cannot compare contents of directories.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string leftPath = selectedItem.LeftNode?.FullPath;
            string rightPath = selectedItem.RightNode?.FullPath;

            if (string.IsNullOrEmpty(leftPath) && string.IsNullOrEmpty(rightPath))
            {
                MessageBox.Show("No valid file paths to compare.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var comparisonHandler = new FileComparisonHandler();
            FileType fileType = comparisonHandler.DetermineFileType(leftPath ?? rightPath);

            try
            {
                leftPath = leftPath ?? CreateTemporaryEmptyFile();
                rightPath = rightPath ?? CreateTemporaryEmptyFile();

                if (fileType == FileType.Text)
                {
                    var comparisonResult = await comparisonHandler.CompareFilesAsync(leftPath, rightPath, fileType);
                    OpenFileComparisonTab(leftPath, rightPath, comparisonResult, fileType);
                }
                else if (fileType == FileType.Image)
                {
                    OpenFileComparisonTab(leftPath, rightPath, null, fileType);
                }
                else
                {
                    MessageBox.Show("Unsupported file type for comparison.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error comparing files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ContextMenu_Properties_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultListView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }
            FileOperations.ShowPropertiesDialog(selectedItem);
        }

        private void ContextMenu_OpenLeftLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is TreeNode selectedNode && selectedNode.LeftNode != null)
            {
                FileOperations.OpenFileLocation(selectedNode.LeftNode.FullPath);
            }
        }

        private void ContextMenu_OpenRightLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is TreeNode selectedNode && selectedNode.RightNode != null)
            {
                FileOperations.OpenFileLocation(selectedNode.RightNode.FullPath);
            }
        }

        private void ContextMenu_CopyPathnames_Click(object sender, RoutedEventArgs e)
        {
            bool isLeft = (sender as MenuItem)?.Parent is ContextMenu contextMenu &&
                          contextMenu == FindResource("LeftContextMenu");

            var paths = new List<string>();
            foreach (TreeNode node in ResultListView.SelectedItems.Cast<TreeNode>())
            {
                string path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            if (paths.Count > 0)
            {
                Clipboard.SetText(string.Join("\n", paths));
            }
            else
            {
                MessageBox.Show("No valid paths selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ContextMenu_CopyFilenames_Click(object sender, RoutedEventArgs e)
        {
            bool isLeft = (sender as MenuItem)?.Parent is ContextMenu contextMenu &&
                          contextMenu == FindResource("LeftContextMenu");

            var filenames = new List<string>();
            foreach (TreeNode node in ResultListView.SelectedItems.Cast<TreeNode>())
            {
                string path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
                if (!string.IsNullOrEmpty(path))
                {
                    filenames.Add(Path.GetFileName(path));
                }
            }

            if (filenames.Count > 0)
            {
                Clipboard.SetText(string.Join("\n", filenames));
            }
            else
            {
                MessageBox.Show("No valid files selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject obj = (DependencyObject)e.OriginalSource;
            while (obj != null && !(obj is ListViewItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }

            if (obj is ListViewItem listViewItem)
            {
                ResultListView.SelectedItem = listViewItem.Content;

                GridView gridView = (GridView)ResultListView.View;
                Point clickPoint = e.GetPosition(ResultListView);
                double currentX = 0;
                int clickedColumnIndex = -1;

                for (int i = 0; i < gridView.Columns.Count; i++)
                {
                    currentX += gridView.Columns[i].ActualWidth;
                    if (clickPoint.X <= currentX)
                    {
                        clickedColumnIndex = i;
                        break;
                    }
                }

                if (clickedColumnIndex >= 0 && clickedColumnIndex <= 2)
                {
                    listViewItem.ContextMenu = (ContextMenu)FindResource("LeftContextMenu");
                }
                else if (clickedColumnIndex >= 4 && clickedColumnIndex <= 6)
                {
                    listViewItem.ContextMenu = (ContextMenu)FindResource("RightContextMenu");
                }
                else
                {
                    listViewItem.ContextMenu = null;
                }

                if (listViewItem.ContextMenu != null)
                {
                    listViewItem.ContextMenu.IsOpen = true;
                }
            }

            e.Handled = true;
        }

        private string GetRelativePath(DirectoryComparer.ComparisonItem item, string leftRoot, string rightRoot)
        {
            string path = "";
            if (item.Left != null && item.Left.FullPath.StartsWith(leftRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = item.Left.FullPath.Substring(leftRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            else if (item.Right != null && item.Right.FullPath.StartsWith(rightRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = item.Right.FullPath.Substring(rightRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            return path;
        }

        private void SetNodeDetails(TreeNode node, DirectoryComparer.ComparisonItem item, string leftRoot, string rightRoot)
        {
            node.LeftNode = item.Left;
            node.RightNode = item.Right;
            node.Status = item.Status;

            if (item.Left != null)
            {
                node.LeftName = item.Left.Name;
                node.LeftSize = FileSystemNode.FormatSize(item.Left.Size);
                node.LeftDate = item.Left.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }

            if (item.Right != null)
            {
                node.RightName = item.Right.Name;
                node.RightSize = FileSystemNode.FormatSize(item.Right.Size);
                node.RightDate = item.Right.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }

            node.StatusSymbol = item.Status switch
            {
                ComparisonStatus.Identical => "=",
                ComparisonStatus.Modified => "≠",
                ComparisonStatus.LeftOnly => "<",
                ComparisonStatus.RightOnly => ">",
                ComparisonStatus.Conflict => "X",
                _ => "?"
            };
        }

        private void OpenFileOperations_Click()
        {
            if (_fileOperationWindow == null || !_fileOperationWindow.IsLoaded)
            {
                _fileOperationWindow = new FileOperationWindow();
                _fileOperationWindow.OnTransferStatusChanged += FileOperation_OnTransferStatusChanged;
                _fileOperationWindow.Closed += (s, args) => _fileOperationWindow = null;
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
            Log.Information(a);
            MessageBox.Show(a);
            Dispatcher.Invoke(() =>
            {
                //StatusText.Text = $"Job {jobId}: {message}";
            });
        }
    }
}