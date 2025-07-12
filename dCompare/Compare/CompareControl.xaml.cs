using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.Diff;
using dCompare.Diff.Models;
using dCompare.FileOperation;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    public partial class CompareControl : UserControl, INotifyPropertyChanged, IRefreshable
    {
        private ObservableCollection<TreeNode> _treeNodes = new();
        private DirectoryComparer _comparer = new DirectoryComparer();
        private ListSortDirection _lastSortDirection;
        private GridViewColumnHeader _lastHeaderClicked;
        private Predicate<object>? _currentFilter = null;   // null for non-filter, means all data
        private List<DirectoryComparer.ComparisonItem> _cachedComparisonResults;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isComparing;
        private double _comparisonProgress;
        private ComparisonAlgorithm _selectedAlgorithm;

        private readonly TreeNodeManager _treeNodeManager;
        private readonly FileOperationHandler _fileOperationHandler;

        private readonly IFileManager _fileManager;
        private readonly IConfigurationService _configService;
        private readonly ILogger<CompareControl> _logger;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public string FullPathsTooltip { get; private set; }

        // ViewModel instances for the path inputs
        public PathInputViewModel LeftPathViewModel { get; }
        public PathInputViewModel RightPathViewModel { get; }

        /// <summary>
        /// Gets or sets the left path, primarily for session management.
        /// Setting this property updates the underlying LeftPathViewModel.Path.
        /// </summary>
        public string LoadedLeftPath
        {
            get => LeftPathViewModel.Path ?? string.Empty;
            set
            {
                LeftPathViewModel.SuppressSuggestionsOnce();
                LeftPathViewModel.Path = value;
            }
        }

        /// <summary>
        /// Gets or sets the right path, primarily for session management.
        /// Setting this property updates the underlying RightPathViewModel.Path.
        /// </summary>
        public string LoadedRightPath
        {
            get => RightPathViewModel.Path ?? string.Empty;
            set
            {
                RightPathViewModel.SuppressSuggestionsOnce();
                RightPathViewModel.Path = value;
            }
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

        public CompareControl(IConfigurationService configService, IFileManager fileManager, ILogger<CompareControl> logger)
        {
            InitializeComponent();

            _fileManager = fileManager;
            _configService = configService;
            _logger = logger;
            _treeNodeManager = new TreeNodeManager(_treeNodes);
            _fileOperationHandler = new FileOperationHandler(_fileManager);

            // Initialize ViewModels
            LeftPathViewModel = new PathInputViewModel();
            RightPathViewModel = new PathInputViewModel();

            // Set DataContext for the input areas
            LeftPathInputArea.DataContext = LeftPathViewModel;
            RightPathInputArea.DataContext = RightPathViewModel;

            // Subscribe to ViewModel Path changes to update tab header
            LeftPathViewModel.PropertyChanged += PathViewModel_PropertyChanged;
            RightPathViewModel.PropertyChanged += PathViewModel_PropertyChanged;

            ResultListView.ItemsSource = _treeNodes;
            ResultListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));
            DataContext = this; // For IsComparing, Progress, SelectedAlgorithm etc.

            Loaded += CompareControl_Loaded;
            LoadConfigurationAsync();
        }

        private void PathViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PathInputViewModel.Path))
            {
                UpdateTabHeader();
            }
        }

        private async void CompareControl_Loaded(object sender, RoutedEventArgs e)
        {
            await PopulateCompareWithMenusAsync();
        }

        private async Task PopulateCompareWithMenusAsync()
        {
            try
            {
                var config = await _configService.LoadConfigurationAsync();
                List<ExternalViewer> viewers = config.Options?.Diff?.Viewer ?? new();

                if (FindResource("LeftContextMenu") is not ContextMenu leftContextMenu ||
                    FindResource("RightContextMenu") is not ContextMenu rightContextMenu)
                    return;

                var leftCompareWithMenu = FindMenuByName(leftContextMenu, "LeftCompareWithMenu");
                var rightCompareWithMenu = FindMenuByName(rightContextMenu, "RightCompareWithMenu");

                if (leftCompareWithMenu == null || rightCompareWithMenu == null) return;

                PopulateViewerMenu(leftCompareWithMenu, viewers);
                PopulateViewerMenu(rightCompareWithMenu, viewers);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to populate Compare With menus: {Message}", ex.Message);
                MessageBox.Show($"Error loading viewers: {ex.Message}", Properties.Resources.ErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private MenuItem? FindMenuByName(ContextMenu menu, string name) =>
            menu.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Name == name);

        private void PopulateViewerMenu(MenuItem parentMenu, List<ExternalViewer> viewers)
        {
            parentMenu.Items.Clear();
            foreach (var viewer in viewers)
            {
                var item = new MenuItem
                {
                    Header = viewer.Name,
                    Tag = viewer,
                    InputGestureText = viewer.Name
                };
                item.Click += ContextMenu_CompareWithViewer_Click;
                parentMenu.Items.Add(item);
            }
        }

        private void LoadConfigurationAsync()
        {
            try
            {
                var defaultAlgorithm = ComparisonAlgorithms.AlgorithmSettings
                    .FirstOrDefault(kvp => kvp.Value.IsDefault && kvp.Value.IsEnabled)
                    .Key;
                SelectedAlgorithm = defaultAlgorithm;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load configuration: {Message}", ex.Message);
                MessageBox.Show($"Error loading configuration: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                SelectedAlgorithm = ComparisonAlgorithm.ModifiedDateAndSize;
            }
        }

        private void UpdateTabHeader()
        {
            // Use Path from ViewModels (which are the source of truth)
            string leftPath = LoadedLeftPath.Trim();
            string rightPath = LoadedRightPath.Trim();

            FullPathsTooltip = $"Left: {leftPath}\nRight: {rightPath}";
            OnPropertyChanged(nameof(FullPathsTooltip));

            string leftName = !string.IsNullOrEmpty(leftPath) ? System.IO.Path.GetFileName(leftPath) : string.Empty;
            string rightName = !string.IsNullOrEmpty(rightPath) ? System.IO.Path.GetFileName(rightPath) : string.Empty;

            // Ensure System.IO.Path is used explicitly, though it's already correct due to `using System.IO;`
            // and previous updates. This is more for a documentation of check.
            // No actual change needed here as it was already System.IO.Path.GetFileName implicitly or explicitly.

            if (string.IsNullOrEmpty(leftName) && !string.IsNullOrEmpty(leftPath)) leftName = leftPath;
            if (string.IsNullOrEmpty(rightName) && !string.IsNullOrEmpty(rightPath)) rightName = rightPath;


            string baseHeader = $"{leftName} - {rightName}";
            if (string.IsNullOrWhiteSpace(leftName) && string.IsNullOrWhiteSpace(rightName))
            {
                //baseHeader = Properties.Resources.NewTabTitle;
                baseHeader = "QQ";
            }
            else if (string.IsNullOrWhiteSpace(leftName))
            {
                baseHeader = $"- {rightName}";
            }
            else if (string.IsNullOrWhiteSpace(rightName))
            {
                baseHeader = $"{leftName} -";
            }


            if (WpfTreeHelperExtensions.FindParent<TabItem>(this) is TabItem tabItem)
            {
                tabItem.Header = IsComparing ? $"(Comparing...) {baseHeader}" : baseHeader;
            }
        }

        public void ChangeTabHeader(string newHeader)
        {
            TabItem? tabItem = WpfTreeHelperExtensions.FindParent<TabItem>(this);
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
                if ((sender as Button)?.Name == "BrowseLeftButton")
                {
                    LeftPathViewModel.Path = folderDialog.FolderName;
                }
                else
                {
                    RightPathViewModel.Path = folderDialog.FolderName;
                }
            }
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public async void Refresh()
        {
            if (IsComparing) { MessageBox.Show("Comparison is already in progress."); return; }

            _treeNodes.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            // Use Path from ViewModels
            string leftDir = LeftPathViewModel.Path?.Trim() ?? string.Empty;
            string rightDir = RightPathViewModel.Path?.Trim() ?? string.Empty;

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
                        progress => Dispatcher.Invoke(() => ComparisonProgress = progress * 100),
                        SelectedAlgorithm),
                    _cancellationTokenSource.Token);

                _cachedComparisonResults = comparisonResults;
                _treeNodeManager.BuildTree(comparisonResults, leftDir, rightDir, maxDepth: 0);

                // UpdateTabHeader is called via PropertyChanged from Path, so this direct call might be redundant
                // or could be used if paths are set programmatically without ViewModel notification.
                UpdateTabHeader();
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

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader headerClicked && headerClicked.Column?.Header is string sortBy)
            {
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

            IsComparing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            string leftDirRoot = LeftPathViewModel.Path?.Trim() ?? string.Empty;
            string rightDirRoot = RightPathViewModel.Path?.Trim() ?? string.Empty;

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
                        _treeNodeManager.LoadChildren(clickedItem, _cachedComparisonResults, LeftPathViewModel.Path?.Trim() ?? string.Empty, RightPathViewModel.Path?.Trim() ?? string.Empty);
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

            string leftDir = LeftPathViewModel.Path?.Trim() ?? string.Empty;
            string rightDir = RightPathViewModel.Path?.Trim() ?? string.Empty;

            //var rootNodes = _treeNodes.Where(n => n.Depth == 0).ToList();
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
        private void ShowDifferent_Click(object sender, RoutedEventArgs e) =>
            SetFilter(node => node.Status == ComparisonStatus.Modified ||
                            node.Status == ComparisonStatus.Conflict ||
                            node.Status == ComparisonStatus.LeftOnly ||
                            node.Status == ComparisonStatus.RightOnly);
        private void ShowIdentical_Click(object sender, RoutedEventArgs e) => SetFilter(node => node.Status == ComparisonStatus.Identical);
        private void ShowLeftUnique_Click(object sender, RoutedEventArgs e) => SetFilter(node => node.Status == ComparisonStatus.LeftOnly);
        private void ShowRightUnique_Click(object sender, RoutedEventArgs e) => SetFilter(node => node.Status == ComparisonStatus.RightOnly);

        private void SetFilter(Predicate<TreeNode>? filter)
        {
            _currentFilter = obj => obj is TreeNode node && (filter == null || filter(node));
            _treeNodeManager.SetCurrentFilter(filter);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
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

            string? leftPath = selectedItem.LeftNode?.FullPath;
            string? rightPath = selectedItem.RightNode?.FullPath;

            if (string.IsNullOrEmpty(leftPath) || string.IsNullOrEmpty(rightPath))
            {
                MessageBox.Show("No valid file paths to compare.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var config = await _configService.LoadConfigurationAsync();
                var comparisonHandler = new FileComparisonHandler(config);
                FileType fileType = comparisonHandler.DetermineFileType(leftPath ?? rightPath);

                leftPath = leftPath ?? CreateTemporaryEmptyFile();
                rightPath = rightPath ?? CreateTemporaryEmptyFile();

                if (fileType == FileType.Text && config?.Options?.Diff?.Text?.Viewer?.Equals("WinMerge", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var winMergeViewer = config?.Options?.Diff?.Viewer?.FirstOrDefault(v => v.Name.Equals("WinMerge", StringComparison.OrdinalIgnoreCase));
                    if (winMergeViewer == null)
                    {
                        MessageBox.Show("WinMerge configuration not found.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string arguments = winMergeViewer.Arguments
                        .Replace("{left}", leftPath)
                        .Replace("{right}", rightPath);

                    ProcessStartInfo startInfo = new()
                    {
                        FileName = winMergeViewer.Path,
                        Arguments = arguments,
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                }
                else if (fileType == FileType.Text && config?.Options?.Diff?.Text?.Viewer?.Equals("DiffPlex", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var comparisonResult = await comparisonHandler.CompareFilesAsync(leftPath, rightPath, fileType);
                    OpenFileComparisonTab(leftPath, rightPath, fileType, comparisonResult);
                }
                else if (fileType == FileType.Image)
                {
                    OpenFileComparisonTab(leftPath, rightPath, fileType, null);
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

        private void ContextMenu_CompareWithViewer_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.Tag is not ExternalViewer viewerConfig)
            {
                MessageBox.Show("Invalid viewer configuration.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ResultListView.SelectedItem is not TreeNode selectedItem)
            {
                MessageBox.Show("Please select an item.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedItem.IsDirectory)
            {
                MessageBox.Show($"Cannot compare directories with {viewerConfig.Name}.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string? leftPath = selectedItem.LeftNode?.FullPath;
            string? rightPath = selectedItem.RightNode?.FullPath;

            if (string.IsNullOrEmpty(leftPath) && string.IsNullOrEmpty(rightPath))
            {
                MessageBox.Show("No valid file paths to compare.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                leftPath = leftPath ?? CreateTemporaryEmptyFile();
                rightPath = rightPath ?? CreateTemporaryEmptyFile();

                string arguments = viewerConfig.Arguments
                    .Replace("{left}", leftPath)
                    .Replace("{right}", rightPath);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = viewerConfig.Path,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching {viewerConfig.Name}: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string CreateTemporaryEmptyFile()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"dCompare_Empty_{Guid.NewGuid()}.txt");
            File.WriteAllText(tempPath, string.Empty);
            return tempPath;
        }

        private void OpenFileComparisonTab(string leftPath, string rightPath, FileType fileType, object? comparisonResult)
        {
            if (WpfTreeHelperExtensions.FindParent<TabItem>(this) is not TabItem parentTab ||
                WpfTreeHelperExtensions.FindParent<TabControl>(parentTab) is not TabControl tabControl)
            {
                MessageBox.Show("Cannot locate parent TabControl or TabItem.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string tabHeader = $"Compare: {System.IO.Path.GetFileName(leftPath)} - {System.IO.Path.GetFileName(rightPath)}";
            TabItem newTab = new TabItem { Header = tabHeader };

            if (fileType == FileType.Text)
            {
                if (comparisonResult is null)
                {
                    MessageBox.Show("Cannot find comparison result.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                newTab.Content = new TextDiffPlex(leftPath, rightPath, comparisonResult);
            }
            else if (fileType == FileType.Image)
            {
                var imageViewer = new ImageDiff(leftPath, rightPath);
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
            string leftDir,
            string rightDir,
            string? confirmationMessage,
            Func<List<TreeNode>, string, string, Task> operationAction)
        {
            if (selectedNodes.Count == 0)
            {
                MessageBox.Show("Please select at least one file or directory for the operation.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                    await operationAction(new List<TreeNode> { node }, leftDir, rightDir);
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
                LeftPathViewModel.Path?.Trim() ?? string.Empty,
                RightPathViewModel.Path?.Trim() ?? string.Empty,
                null,
                _fileOperationHandler.CopyToLeft);
        }

        private async void ContextMenu_CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                LeftPathViewModel.Path?.Trim() ?? string.Empty,
                RightPathViewModel.Path?.Trim() ?? string.Empty,
                null,
                _fileOperationHandler.CopyToRight);
        }

        private async void ContextMenu_MoveToLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                LeftPathViewModel.Path?.Trim() ?? string.Empty,
                RightPathViewModel.Path?.Trim() ?? string.Empty,
                null,
                _fileOperationHandler.MoveToLeft);
        }

        private async void ContextMenu_MoveToRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                LeftPathViewModel.Path?.Trim() ?? string.Empty,
                RightPathViewModel.Path?.Trim() ?? string.Empty,
                null,
                _fileOperationHandler.MoveToRight);
        }

        private async void ContextMenu_DeleteLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                LeftPathViewModel.Path?.Trim() ?? string.Empty,
                RightPathViewModel.Path?.Trim() ?? string.Empty,
                "Are you sure you want to delete the selected items from the left directory?",
                async (nodes, leftDir, rightDir) =>
                {
                    var items = _fileOperationHandler.GetItemsForDeletion(nodes, true);
                    if (items.Count > 0) await _fileOperationHandler.DeleteAsync(items);
                });
        }

        private async void ContextMenu_DeleteRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(
                selectedNodes,
                LeftPathViewModel.Path?.Trim() ?? string.Empty,
                RightPathViewModel.Path?.Trim() ?? string.Empty,
                "Are you sure you want to delete the selected items from the right directory?",
                async (nodes, leftDir, rightDir) =>
                {
                    var items = _fileOperationHandler.GetItemsForDeletion(nodes, false);
                    if (items.Count > 0) await _fileOperationHandler.DeleteAsync(items);
                });
        }

        private async void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is not TreeNode selectedItem)
            {
                MessageBox.Show("Please select an item to rename.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            bool isLeft = contextMenu == FindResource("LeftContextMenu");

            string? sourcePath = isLeft ? selectedItem.LeftNode?.FullPath : selectedItem.RightNode?.FullPath;
            if (string.IsNullOrEmpty(sourcePath))
            {
                MessageBox.Show("The selected item does not have a valid path to rename.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string currentName = System.IO.Path.GetFileName(sourcePath);
            string newName = Interaction.InputBox("Enter the new name for the item:", "Rename", currentName);

            if (string.IsNullOrWhiteSpace(newName) || newName == currentName)
            {
                return; // User cancelled or entered the same name
            }

            try
            {
                // Find parent BEFORE the operation, as the selectedItem's path will become invalid.
                string parentRelativePath = Path.GetDirectoryName(selectedItem.RelativePath) ?? string.Empty;
                TreeNode? parentNode = null;
                // A non-empty parent path indicates the item is not at the root.
                if (!string.IsNullOrEmpty(parentRelativePath))
                {
                    parentNode = _treeNodes.FirstOrDefault(n => n.RelativePath.Equals(parentRelativePath, StringComparison.OrdinalIgnoreCase));
                }

                // Execute the rename operation using the new testable method
                _fileOperationHandler.RenameFileOrDirectory(sourcePath, newName);

                // Refresh the UI to reflect the change.
                if (parentNode != null)
                {
                    // Preserve the expanded state of the tree for a better user experience.
                    var previouslyExpandedPaths = new HashSet<string>(
                        _treeNodes.Where(n => n.IsDirectory && n.IsExpanded).Select(n => n.RelativePath)
                    );
                    await RefreshNodeAsync(parentNode, previouslyExpandedPaths);
                }
                else
                {
                    Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error renaming item: {ex.Message}", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                string? path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
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
                string? path = isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath;
                if (!string.IsNullOrEmpty(path))
                {
                    // This was already correct from a previous fix.
                    filenames.Add(System.IO.Path.GetFileName(path));
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

        // Event Handlers for Path TextBoxes and Suggestion ListBoxes
        private void PathBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            PathInputViewModel? vm = null;
            ListBox? suggestionsListBox = null;

            if (textBox == LeftPathBox)
            {
                vm = LeftPathViewModel;
                suggestionsListBox = LeftSuggestionsListBox;
            }
            else if (textBox == RightPathBox)
            {
                vm = RightPathViewModel;
                suggestionsListBox = RightSuggestionsListBox;
            }

            if (vm == null || suggestionsListBox == null) return;

            switch (e.Key)
            {
                case Key.Escape:
                    vm.IsSuggestionsOpen = false;
                    e.Handled = true;
                    break;
                case Key.Down:
                    if (vm.IsSuggestionsOpen && vm.Suggestions.Any())
                    {
                        suggestionsListBox.Focus();
                        if (suggestionsListBox.SelectedIndex < 0 && suggestionsListBox.Items.Count > 0)
                        {
                            suggestionsListBox.SelectedIndex = 0;
                        }
                        e.Handled = true;
                    }
                    break;
                case Key.Enter:
                    if (vm.IsSuggestionsOpen && suggestionsListBox.SelectedItem != null)
                    {
                        vm.SelectedSuggestion = suggestionsListBox.SelectedItem as string;
                        // Path and IsSuggestionsOpen are updated by SelectedSuggestion setter
                        e.Handled = true;
                        textBox.Focus(); // Move focus back to textbox
                        textBox.CaretIndex = textBox.Text.Length; // Move cursor to end
                    }
                    else if (vm.IsSuggestionsOpen) // If popup is open but nothing selected, close it
                    {
                        vm.IsSuggestionsOpen = false;
                        e.Handled = true;
                    }
                    // If popup is not open, Enter key should trigger comparison (handled elsewhere or default behavior)
                    break;
            }
        }

        private void SuggestionsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listBox = sender as ListBox;
            PathInputViewModel? vm = null;
            TextBox? targetTextBox = null;

            if (listBox == LeftSuggestionsListBox)
            {
                vm = LeftPathViewModel;
                targetTextBox = LeftPathBox;
            }
            else if (listBox == RightSuggestionsListBox)
            {
                vm = RightPathViewModel;
                targetTextBox = RightPathBox;
            }

            if (vm == null || targetTextBox == null) return;

            if (e.Key == Key.Enter)
            {
                if (listBox.SelectedItem != null)
                {
                    vm.SelectedSuggestion = listBox.SelectedItem as string;
                    // Path and IsSuggestionsOpen are updated by SelectedSuggestion setter in VM
                }
                targetTextBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.IsSuggestionsOpen = false;
                targetTextBox.Focus();
                e.Handled = true;
            }
        }

        private void PathBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            PathInputViewModel? vm = null;
            Popup? popup = null;

            if (textBox == LeftPathBox)
            {
                vm = LeftPathViewModel;
                popup = LeftSuggestionsPopup;
            }
            else if (textBox == RightPathBox)
            {
                vm = RightPathViewModel;
                popup = RightSuggestionsPopup;
            }

            if (vm == null || popup == null) return;

            // Check if focus is moving to the popup or its child (the ListBox)
            // If StaysOpen is false, this check is often sufficient.
            // For more complex scenarios, you might need to check relatedTarget if Popup.StaysOpen=true
            if (!popup.IsKeyboardFocusWithin && !popup.IsMouseOver)
            {
                 // Add a small delay to allow selection to process if a click on popup item caused focus loss
                Dispatcher.BeginInvoke(new Action(() => {
                    if (!popup.IsKeyboardFocusWithin && !popup.IsMouseOver && !(popup.Child as ListBox)?.IsKeyboardFocusWithin == true)
                    {
                        vm.IsSuggestionsOpen = false;
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
    }
}