using dCompare.Configuration;
using dCompare.Diff;
using dCompare.FileOperation;
using dCompare.FileOperation.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
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
    public enum FileOperationType
    {
        CopyToLeft,
        CopyToRight,
        MoveToLeft,
        MoveToRight,
        DeleteLeft,
        DeleteRight
    }

    public partial class UcCompare : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<TreeNode> _treeNodes = new();
        private DirectoryComparer _comparer = new DirectoryComparer();
        private ListSortDirection _lastSortDirection;
        private GridViewColumnHeader _lastHeaderClicked;
        private Predicate<object> _currentFilter = null;
        private List<DirectoryComparer.ComparisonItem> _cachedComparisonResults;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isComparing;
        private double _comparisonProgress;
        private ComparisonAlgorithm _selectedAlgorithm;

        private FileOperationWindow _fileOperationWindow;
        //private readonly FileManager _fileManager = new FileManager(new Kernel32FileCopyStrategy());
        private readonly TreeNodeManager _treeNodeManager;
        private readonly FileOperationHandler _fileOperationHandler;

        private readonly IFileManager _fileManager;
        private readonly IConfigurationService _configService;

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

        public ComparisonAlgorithm SelectedAlgorithm
        {
            get => _selectedAlgorithm;
            set
            {
                _selectedAlgorithm = value;
                OnPropertyChanged(nameof(SelectedAlgorithm));
            }
        }

        public UcCompare(IFileManager fileManager, IConfigurationService configService)
        {
            InitializeComponent();

            _fileManager = fileManager;
            _configService = configService;

            _treeNodeManager = new TreeNodeManager(_treeNodes);
            _fileOperationHandler = new FileOperationHandler(_fileManager, _treeNodeManager);

            ResultListView.ItemsSource = _treeNodes;
            ResultListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeaderClickedHandler));
            DataContext = this;

            // Load default algorithm from configuration
            LoadConfigurationAsync();
        }
        private async void LoadConfigurationAsync()
        {
            try
            {
                var config = await _configService.LoadConfigurationAsync();
                SelectedAlgorithm = config.Options.Compare.Algorithm; // Set default algorithm
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load configuration: {Message}", ex.Message);
                MessageBox.Show($"Error loading configuration: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                SelectedAlgorithm = ComparisonAlgorithm.ModifiedDateAndSize; // Fallback default
            }
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
                List<DirectoryComparer.ComparisonItem> comparisonResults = await Task.Run(() =>
                    _comparer.Compare(leftDir, rightDir, _cancellationTokenSource.Token,
                        progress => Dispatcher.Invoke(() => ComparisonProgress = progress * 100), SelectedAlgorithm),
                    _cancellationTokenSource.Token);

                _cachedComparisonResults = comparisonResults;
                _treeNodeManager.BuildTree(comparisonResults, leftDir, rightDir, maxDepth: 0);

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

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked)
            {
                string sortBy = headerClicked.Column.Header as string;
                ListSortDirection direction = _lastHeaderClicked == headerClicked && _lastSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

                ICollectionView view = CollectionViewSource.GetDefaultView(_treeNodes);
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

        public async Task RefreshNodeAsync(TreeNode nodeToRefresh, HashSet<string> previouslyExpandedPaths = null)
        {
            if (IsComparing) { MessageBox.Show("Comparison is already in progress."); return; }
            //if (!nodeToRefresh.IsDirectory) { return; }

            IsComparing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            string leftDirRoot = LeftPathBox.Text.Trim();
            string rightDirRoot = RightPathBox.Text.Trim();

            try
            {
                // 1. Re-compare just the subdirectory to get the new items.
                List<DirectoryComparer.ComparisonItem> refreshedItems = await Task.Run(() =>
                    _comparer.CompareSubtree(leftDirRoot, rightDirRoot, nodeToRefresh.RelativePath,
                        _cancellationTokenSource.Token, SelectedAlgorithm),
                    _cancellationTokenSource.Token);

                // 2. Update the master cache (_cachedComparisonResults).
                _cachedComparisonResults.RemoveAll(item =>
                    _treeNodeManager.GetRelativePath(item, leftDirRoot, rightDirRoot)
                                   .StartsWith(nodeToRefresh.RelativePath, StringComparison.OrdinalIgnoreCase));

                _cachedComparisonResults.AddRange(refreshedItems);

                // 3. Update the UI collection (_treeNodes), passing the expanded state information.
                _treeNodeManager.RefreshSubtreeInView(nodeToRefresh, _cachedComparisonResults, leftDirRoot, rightDirRoot, previouslyExpandedPaths);

                // 4. Re-apply any active filter to the updated view.
                ApplyFilter();
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Refresh was canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during refresh: {ex.Message}");
            }
            finally
            {
                IsComparing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }
        private void ResultListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
                        _treeNodeManager.RemoveChildren(clickedItem);
                    }
                    else
                    {
                        clickedItem.IsExpanded = true;
                        _treeNodeManager.LoadChildren(clickedItem, _cachedComparisonResults, LeftPathBox.Text.Trim(), RightPathBox.Text.Trim());
                    }
                }
                else
                {
                    CompareFileContents(clickedItem);
                }
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            if (_cachedComparisonResults == null) return;

            string leftDir = LeftPathBox.Text.Trim();
            string rightDir = RightPathBox.Text.Trim();

            var rootNodes = _treeNodes.Where(n => n.Depth == 0).ToList();
            _treeNodes.Clear();

            List<TreeNode> allNodes = _treeNodeManager.CreateFlatTreeStructure(_cachedComparisonResults, leftDir, rightDir);

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
            _treeNodeManager.SetCurrentFilter(filter); // Pass filter to TreeNodeManager
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            // CollectionViewSource.GetDefaultView(_treeNodes).Refresh();
            ICollectionView? view = CollectionViewSource.GetDefaultView(_treeNodes);
            view.Filter = _currentFilter;
        }

        private async void CompareFileContents(TreeNode selectedItem)
        {
            if (selectedItem.IsDirectory)
            {
                MessageBox.Show("Cannot compare contents of directories.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string leftPath = selectedItem.LeftNode?.FullPath;
            string rightPath = selectedItem.RightNode?.FullPath;

            if (string.IsNullOrEmpty(leftPath) && string.IsNullOrEmpty(rightPath))
            {
                MessageBox.Show("No valid file paths to compare.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Unsupported file type for comparison.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error comparing files: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Cannot find parent TabControl.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Unsupported file type for comparison.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            tabControl.Items.Add(newTab);
            tabControl.SelectedItem = newTab;
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
                // Only add to selection if not already selected to preserve multi-selection
                if (!ResultListView.SelectedItems.Contains(listViewItem.Content))
                {
                    ResultListView.SelectedItems.Add(listViewItem.Content);
                }

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

                // Mark the event as handled to prevent default selection behavior
                e.Handled = true;
            }
        }

        private void ContextMenu_CompareContents_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultListView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("Please select an item.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CompareFileContents(selectedItem);
        }

        private async Task ProcessFileOperationsAsync(
    List<TreeNode> selectedNodes,
    FileOperationType operationType,
    string leftDir,
    string rightDir,
    string confirmationMessage,
    Func<List<TreeNode>, string, string, Task> operationAction,
    Func<List<TreeNode>, bool, List<(string Path, SourceType Type)>> getItemsForDeletion = null)
        {
            if (selectedNodes.Count == 0)
            {
                MessageBox.Show("Please select at least one file or directory for the operation.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // *** STEP 1: Capture the state of all expanded nodes BEFORE the operation. ***
            var previouslyExpandedPaths = new HashSet<string>(
                _treeNodes.Where(n => n.IsDirectory && n.IsExpanded)
                          .Select(n => n.RelativePath)
            );

            try
            {
                if (confirmationMessage != null)
                {
                    var result = MessageBox.Show(
                        confirmationMessage,
                        "Confirm Operation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                foreach (var node in selectedNodes)
                {
                    if (operationType == FileOperationType.DeleteLeft || operationType == FileOperationType.DeleteRight)
                    {
                        bool isLeft = operationType == FileOperationType.DeleteLeft;
                        var items = getItemsForDeletion(new List<TreeNode> { node }, isLeft);
                        if (items.Count > 0) await _fileOperationHandler.DeleteAsync(items);
                    }
                    else
                    {
                        await operationAction(new List<TreeNode> { node }, leftDir, rightDir);
                    }
                }

                var nodesToRefresh = new Dictionary<string, TreeNode>();
                var queue = new Queue<TreeNode>(selectedNodes);

                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    if (node == null || nodesToRefresh.ContainsKey(node.RelativePath)) continue;

                    nodesToRefresh.Add(node.RelativePath, node);

                    if (node.Depth > 0)
                    {
                        string parentPath = Path.GetDirectoryName(node.RelativePath) ?? string.Empty;
                        var parentNode = _treeNodes.FirstOrDefault(n => n.RelativePath.Equals(parentPath, StringComparison.OrdinalIgnoreCase));
                        if (parentNode != null)
                        {
                            queue.Enqueue(parentNode);
                        }
                    }
                }

                var orderedNodesToRefresh = nodesToRefresh.Values.OrderByDescending(n => n.Depth);
                foreach (var nodeToRefresh in orderedNodesToRefresh)
                {
                    // *** Pass the captured state to the refresh method. ***
                    await RefreshNodeAsync(nodeToRefresh, previouslyExpandedPaths);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"The operation failed and the view may not be fully refreshed. Details: {ex.Message}", "Operation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ContextMenu_CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                FileOperationType.CopyToLeft,
                LeftPathBox.Text.Trim(),
                RightPathBox.Text.Trim(),
                null,
                _fileOperationHandler.CopyToLeft);
        }

        private async void ContextMenu_CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                FileOperationType.CopyToRight,
                LeftPathBox.Text.Trim(),
                RightPathBox.Text.Trim(),
                null,
                _fileOperationHandler.CopyToRight);
        }

        private async void ContextMenu_MoveToLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                FileOperationType.MoveToLeft,
                LeftPathBox.Text.Trim(),
                RightPathBox.Text.Trim(),
                null,
                _fileOperationHandler.MoveToLeft);
        }

        private async void ContextMenu_MoveToRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                FileOperationType.MoveToRight,
                LeftPathBox.Text.Trim(),
                RightPathBox.Text.Trim(),
                null,
                _fileOperationHandler.MoveToRight);
        }

        private async void ContextMenu_DeleteLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                FileOperationType.DeleteLeft,
                LeftPathBox.Text.Trim(),
                RightPathBox.Text.Trim(),
                "Are you sure you want to delete the selected items from the left directory?",
                null,
                _fileOperationHandler.GetItemsForDeletion);
        }

        private async void ContextMenu_DeleteRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                FileOperationType.DeleteRight,
                LeftPathBox.Text.Trim(),
                RightPathBox.Text.Trim(),
                "Are you sure you want to delete the selected items from the right directory?",
                null,
                _fileOperationHandler.GetItemsForDeletion);
        }

        private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultListView.SelectedItem as TreeNode;
            bool isLeft = (sender as MenuItem)?.Parent is ContextMenu contextMenu &&
                          contextMenu == FindResource("LeftContextMenu");
            _fileOperationHandler.Rename(selectedItem, isLeft,
                LeftPathBox.Text.Trim(), RightPathBox.Text.Trim());
        }

        private void ContextMenu_Properties_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultListView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("Please select an item.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("No valid paths selected.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("No valid files selected.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            Log.Information($"UcCompare: complete copy or move or delete: {a}");
            MessageBox.Show(a);
        }
    }
}