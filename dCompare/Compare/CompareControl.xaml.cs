using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.FileOperation;
using dCompare.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace dCompare.Compare
{
    public enum FileOperationType
    {
        CopyToLeft,
        CopyToRight,
        MoveToLeft,
        MoveToRight,
        DeleteLeft,
        DeleteRight
    }

    public partial class CompareControl : UserControl, INotifyPropertyChanged, IRefreshable, ITool
    {
        private readonly IConfigurationService _configService;
        private readonly ILogger<CompareControl> _logger;
        private readonly IFileManager _fileManager;
        private readonly ToolHandler _toolHandler;

        private DirectoryComparer _comparer = new();
        private List<DirectoryComparer.ComparisonItem> _cachedComparisonResults;
        private ObservableCollection<TreeNode> _treeNodes = new();
        private ListSortDirection _lastSortDirection;
        private GridViewColumnHeader _lastHeaderClicked;
        private Predicate<object>? _currentFilter = null;   // null for non-filter, means all data
        private CancellationTokenSource _cancellationTokenSource;

        private readonly TreeNodeManager _treeNodeManager;
        private readonly FileOperationHandler _fileOperationHandler;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public string FullPathsTooltip { get; private set; }

        public CompareControlViewModel ViewModel { get; }

        public CompareControl(IConfigurationService configService, IFileManager fileManager, ILogger<CompareControl> logger)
        {
            InitializeComponent();

            _logger = logger;
            _fileManager = fileManager;
            _configService = configService;
            _toolHandler = new ToolHandler();
            _treeNodeManager = new TreeNodeManager(_treeNodes);
            _fileOperationHandler = new FileOperationHandler(_fileManager);

            // Initialize ViewModel
            ViewModel = new CompareControlViewModel();
            DataContext = ViewModel;

            // Subscribe to ViewModel Path changes to update tab header
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            ResultListView.ItemsSource = _treeNodes;
            ResultListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(ColumnHeader_Click));

            SetFilter(null);
        }

        public CompareControl(IConfigurationService configService, IFileManager fileManager, ILogger<CompareControl> logger, string leftPath, string rightPath)
            : this(configService, fileManager, logger)
        {
            // TODO This is for archive file only, may consider using other solution
            ViewModel.LeftPath = leftPath;
            ViewModel.RightPath = rightPath;
            this.Refresh();
        }
        private async void CompareControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(ViewModel.LeftPath))
                {
                    var defaultAlgorithm = (await _configService.LoadConfigurationAsync())
                        .Options.Compare.Algorithms
                        .FirstOrDefault(a => a.Value.IsDefault && a.Value.IsEnabled)
                        .Key;
                    ((CompareControlViewModel)DataContext).SelectedAlgorithm = defaultAlgorithm;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load configuration: {Message}", ex.Message);
                MessageBox.Show(string.Format(Properties.Resources.ErrorLoadingConfiguration, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                ((CompareControlViewModel)DataContext).SelectedAlgorithm = ComparisonAlgorithm.ModifiedDateAndSize;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CompareControlViewModel.LeftPath) || e.PropertyName == nameof(CompareControlViewModel.RightPath))
            {
                UpdateTabHeader();
            }
            else if (e.PropertyName == nameof(CompareControlViewModel.SearchText))
            {
                ApplyFilter();
            }
            else if (e.PropertyName == nameof(CompareControlViewModel.IsComparing))
            {
                UpdateTabHeader();
            }
        }

        private void UpdateTabHeader()
        {
            // Use Path from ViewModels (which are the source of truth)
            string leftPath = ViewModel.LeftPath.Trim();
            string rightPath = ViewModel.RightPath.Trim();

            FullPathsTooltip = string.Format(Properties.Resources.TooltipFullPaths, leftPath, rightPath);
            OnPropertyChanged(nameof(FullPathsTooltip));

            string leftName = !string.IsNullOrEmpty(leftPath) ? Path.GetFileName(leftPath) : string.Empty;
            string rightName = !string.IsNullOrEmpty(rightPath) ? Path.GetFileName(rightPath) : string.Empty;

            if (string.IsNullOrEmpty(leftName) && !string.IsNullOrEmpty(leftPath)) leftName = leftPath;
            if (string.IsNullOrEmpty(rightName) && !string.IsNullOrEmpty(rightPath)) rightName = rightPath;

            string baseHeader = $"{leftName} - {rightName}";
            if (string.IsNullOrWhiteSpace(leftName) && string.IsNullOrWhiteSpace(rightName))
            {
                //baseHeader = Properties.Resources.NewTabTitle;
                baseHeader = "TODO";
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
                tabItem.Header = ((CompareControlViewModel)DataContext).IsComparing ? string.Format(Properties.Resources.ComparingTabHeader, baseHeader) : baseHeader;
            }
        }

        //public void ChangeTabHeader(string newHeader)
        //{
        //    TabItem? tabItem = WpfTreeHelperExtensions.FindParent<TabItem>(this);
        //    if (tabItem != null && !IsComparing)
        //    {
        //        tabItem.Header = newHeader;
        //    }
        //}

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = Properties.Resources.SelectFolderOrArchiveTitle,
                Filter = Properties.Resources.ArchiveFileFilter,
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Folder Selection."
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                // If the user selected a directory, the file name will be "Folder Selection."
                if (selectedPath.Contains("Folder Selection."))
                {
            selectedPath = Path.GetDirectoryName(selectedPath) ?? selectedPath;
                }

                if (sender is Button { Name: "BrowseLeftButton" })
                {
                    ViewModel.LeftPath = selectedPath;
                }
                else
                {
                    ViewModel.RightPath = selectedPath;
                }
            }
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        public async void Refresh()
        {
            var viewModel = (CompareControlViewModel)DataContext;
            if (viewModel.IsComparing) { MessageBox.Show(Properties.Resources.ComparisonInProgress); return; }

            _treeNodes.Clear();
            _cancellationTokenSource = new CancellationTokenSource();

            // Use Path from ViewModels
            string leftDir = ViewModel.LeftPath?.Trim() ?? string.Empty;
            string rightDir = ViewModel.RightPath?.Trim() ?? string.Empty;

            if ((!Directory.Exists(leftDir) && !File.Exists(leftDir)) && !string.IsNullOrEmpty(leftDir))
            {
                MessageBox.Show(Properties.Resources.EnterValidSourcePath);
                return;
            }

            if ((!Directory.Exists(rightDir) && !File.Exists(rightDir)) && !string.IsNullOrEmpty(rightDir))
            {
                MessageBox.Show(Properties.Resources.EnterValidTargetPath);
                return;
            }

            viewModel.IsComparing = true;
            viewModel.ComparisonProgress = 0;
            try
            {
                List<DirectoryComparer.ComparisonItem> comparisonResults = await Task.Run(() =>
                    _comparer.Compare(leftDir, rightDir, _cancellationTokenSource.Token,
                        progress => Dispatcher.Invoke(() => viewModel.ComparisonProgress = progress * 100),
                        viewModel.SelectedAlgorithm),
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
                MessageBox.Show(Properties.Resources.ComparisonCanceled);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorDuringComparison, ex.Message));
            }
            finally
            {
                viewModel.IsComparing = false;
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
            var viewModel = (CompareControlViewModel)DataContext;
            if (viewModel.IsComparing) { MessageBox.Show(Properties.Resources.ComparisonInProgress); return; }

            viewModel.IsComparing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            string leftDirRoot = ViewModel.LeftPath?.Trim() ?? string.Empty;
            string rightDirRoot = ViewModel.RightPath?.Trim() ?? string.Empty;

            try
            {
                // 1. Re-compare just the subdirectory to get the new items.
                List<DirectoryComparer.ComparisonItem> refreshedItems = await Task.Run(() =>
                    _comparer.CompareSubtree(leftDirRoot, rightDirRoot, nodeToRefresh.RelativePath,
                        _cancellationTokenSource.Token, viewModel.SelectedAlgorithm),
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
                MessageBox.Show(Properties.Resources.RefreshCanceled);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorDuringRefresh, ex.Message));
            }
            finally
            {
                viewModel.IsComparing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        #region Filter
        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            if (_cachedComparisonResults == null) return;

            string leftDir = ViewModel.LeftPath?.Trim() ?? string.Empty;
            string rightDir = ViewModel.RightPath?.Trim() ?? string.Empty;

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
            _currentFilter = obj =>
            {
                if (obj is not TreeNode node) return false;

                bool matchesFilter = filter == null || filter(node);
                if (!matchesFilter) return false;

                if (string.IsNullOrEmpty(ViewModel.SearchText) || ViewModel.SearchText == Properties.Resources.SearchWatermark)
                {
                    return true;
                }

                var searchText = ViewModel.SearchText;
                return (node.LeftNode != null && node.LeftNode.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                       (node.RightNode != null && node.RightNode.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            };

            _treeNodeManager.SetCurrentFilter(filter, ViewModel.SearchText);
            ApplyFilter();
        }
        private void ApplyFilter()
        {
            ICollectionView? view = CollectionViewSource.GetDefaultView(ResultListView.ItemsSource);
            if (view != null)
            {
                view.Filter = _currentFilter;
            }
        }
        #endregion
        private void ProcessToolAction(object sender, Action<TreeNode, ToolOptions> action)
        {
            if (sender is not MenuItem menuItem || menuItem.Tag is not ToolOptions tool)
            {
                MessageBox.Show(Properties.Resources.InvalidToolConfiguration, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ResultListView.SelectedItem is not TreeNode selectedItem)
            {
                MessageBox.Show(Properties.Resources.PleaseSelectItem, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            action(selectedItem, tool);
        }

        #region Path Suggestion
        private void PathBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            bool isLeft = textBox == LeftPathBox;
            var suggestionsListBox = isLeft ? LeftSuggestionsListBox : RightSuggestionsListBox;
            var suggestions = isLeft ? ViewModel.LeftSuggestions : ViewModel.RightSuggestions;
            var isSuggestionsOpen = isLeft ? ViewModel.IsLeftSuggestionsOpen : ViewModel.IsRightSuggestionsOpen;

            if (suggestionsListBox == null) return;

            switch (e.Key)
            {
                case Key.Escape:
                    if (isLeft) ViewModel.IsLeftSuggestionsOpen = false; else ViewModel.IsRightSuggestionsOpen = false;
                    e.Handled = true;
                    break;
                case Key.Down:
                    if (isSuggestionsOpen && suggestions.Count != 0)
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
                    if (isSuggestionsOpen && suggestionsListBox.SelectedItem != null)
                    {
                        if (isLeft) ViewModel.SelectedLeftSuggestion = (string)suggestionsListBox.SelectedItem;
                        else ViewModel.SelectedRightSuggestion = (string)suggestionsListBox.SelectedItem;
                        e.Handled = true;
                        textBox.Focus();
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                    else if (isSuggestionsOpen)
                    {
                        if (isLeft) ViewModel.IsLeftSuggestionsOpen = false; else ViewModel.IsRightSuggestionsOpen = false;
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void SuggestionsListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            bool isLeft = listBox == LeftSuggestionsListBox;
            var targetTextBox = isLeft ? LeftPathBox : RightPathBox;

            if (targetTextBox == null) return;

            if (e.Key == Key.Enter)
            {
                if (listBox.SelectedItem != null)
                {
                    if (isLeft) ViewModel.SelectedLeftSuggestion = (string)listBox.SelectedItem;
                    else ViewModel.SelectedRightSuggestion = (string)listBox.SelectedItem;
                }
                targetTextBox.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (isLeft) ViewModel.IsLeftSuggestionsOpen = false;
                else ViewModel.IsRightSuggestionsOpen = false;
                targetTextBox.Focus();
                e.Handled = true;
            }
        }

        private void PathBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            bool isLeft = textBox == LeftPathBox;
            var popup = isLeft ? LeftSuggestionsPopup : RightSuggestionsPopup;

            if (popup == null) return;

            if (!popup.IsKeyboardFocusWithin && !popup.IsMouseOver)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!popup.IsKeyboardFocusWithin && !popup.IsMouseOver && !(popup.Child as ListBox)?.IsKeyboardFocusWithin == true)
                    {
                        if (isLeft) ViewModel.IsLeftSuggestionsOpen = false;
                        else ViewModel.IsRightSuggestionsOpen = false;
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
        #endregion Path suggestion

        #region ConextMenuOperations
        private async void ListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject obj = (DependencyObject)e.OriginalSource;
            while (obj != null && obj is not ListViewItem)
            {
                obj = VisualTreeHelper.GetParent(obj);
            }

            if (obj is ListViewItem listViewItem && listViewItem.Content is TreeNode treeNode)
            {
                // Only add to selection if not already selected to preserve multi-selection
                if (!ResultListView.SelectedItems.Contains(listViewItem.Content))
                {
                    ResultListView.SelectedItems.Add(listViewItem.Content);
                }

                await PopulateMenusForNodeAsync(treeNode);

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
            if (ResultListView.SelectedItem is not TreeNode selectedItem)
            {
                MessageBox.Show(Properties.Resources.PleaseSelectItem, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            CompareFileAsync(selectedItem);
        }
        private void ContextMenu_ViewFile_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is not TreeNode selectedItem)
            {
                MessageBox.Show(Properties.Resources.PleaseSelectItem, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ViewFileAsync(selectedItem);
        }
        private async Task ProcessFileOperationsAsync(List<TreeNode> selectedNodes, string destDir, FileOperationType operation)
        {
            if (selectedNodes.Count == 0)
            {
                MessageBox.Show(Properties.Resources.PleaseSelectFileOrDirectory, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HashSet<string> previouslyExpandedPaths = new(
                _treeNodes.Where(n => n.IsDirectory && n.IsExpanded)
                          .Select(n => n.RelativePath)
            );

            try
            {
                if (operation == FileOperationType.DeleteLeft || operation == FileOperationType.DeleteRight)
                {
                    var result = MessageBox.Show(Properties.Resources.ConfirmDelete, Properties.Resources.ConfirmOperation, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes) { return; }
                }
                foreach (var node in selectedNodes)
                {
                    //await operationAction(new List<TreeNode> { node }, destDir);
                    switch (operation)
                    {
                        case FileOperationType.CopyToLeft:
                            await _fileOperationHandler.CopyToLeft(new List<TreeNode> { node }, destDir);
                            break;
                        case FileOperationType.CopyToRight:
                            await _fileOperationHandler.CopyToRight(new List<TreeNode> { node }, destDir);
                            break;
                        case FileOperationType.MoveToLeft:
                            await _fileOperationHandler.MoveToLeft(new List<TreeNode> { node }, destDir);
                            break;
                        case FileOperationType.MoveToRight:
                            await _fileOperationHandler.MoveToRight(new List<TreeNode> { node }, destDir);
                            break;
                        case FileOperationType.DeleteLeft:
                            var items = _fileOperationHandler.GetItemsForDeletion(new List<TreeNode> { node }, true);
                            if (items.Count > 0) await _fileOperationHandler.DeleteAsync(items);
                            break;
                        case FileOperationType.DeleteRight:
                            var items2 = _fileOperationHandler.GetItemsForDeletion(new List<TreeNode> { node }, false);
                            if (items2.Count > 0) await _fileOperationHandler.DeleteAsync(items2);
                            break;
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
                MessageBox.Show(string.Format(Properties.Resources.OperationFailedDetails, ex.Message), Properties.Resources.OperationFailed, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void ContextMenu_CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(selectedNodes, ViewModel.LeftPath?.Trim() ?? string.Empty, FileOperationType.CopyToLeft);
        }
        private async void ContextMenu_CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(selectedNodes, ViewModel.RightPath?.Trim() ?? string.Empty, FileOperationType.CopyToRight);
        }
        private async void ContextMenu_MoveToLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(selectedNodes, ViewModel.LeftPath?.Trim() ?? string.Empty, FileOperationType.MoveToLeft);
        }
        private async void ContextMenu_MoveToRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(selectedNodes, ViewModel.RightPath?.Trim() ?? string.Empty, FileOperationType.MoveToRight);
        }
        private async void ContextMenu_DeleteLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(selectedNodes, string.Empty, FileOperationType.DeleteLeft);
        }
        private async void ContextMenu_DeleteRight_Click(object sender, RoutedEventArgs e)
        {
            List<TreeNode> selectedNodes = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            await ProcessFileOperationsAsync(selectedNodes, string.Empty, FileOperationType.DeleteRight);
        }
        private async void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is not TreeNode selectedItem)
            {
                MessageBox.Show(Properties.Resources.PleaseSelectItemToRename, Properties.Resources.SelectionRequired, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var menuItem = sender as MenuItem;
            var contextMenu = menuItem?.Parent as ContextMenu;
            bool isLeft = contextMenu == FindResource("LeftContextMenu");

            string? sourcePath = isLeft ? selectedItem.LeftNode?.FullPath : selectedItem.RightNode?.FullPath;
            if (string.IsNullOrEmpty(sourcePath))
            {
                MessageBox.Show(Properties.Resources.InvalidPathToRename, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string currentName = Path.GetFileName(sourcePath);
            string newName = Interaction.InputBox(Properties.Resources.EnterNewName, Properties.Resources.Rename, currentName);

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
                MessageBox.Show(string.Format(Properties.Resources.ErrorRenamingItem, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ContextMenu_Properties_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is not TreeNode selectedItem)
            {
                MessageBox.Show(Properties.Resources.PleaseSelectItem, Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            FileOperationHandler.ShowPropertiesDialog(selectedItem);
        }
        private void ContextMenu_OpenLeftLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is TreeNode selectedNode && selectedNode.LeftNode != null)
            {
                FileOperationHandler.OpenFileLocation(selectedNode.LeftNode.FullPath);
            }
        }
        private void ContextMenu_OpenRightLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ResultListView.SelectedItem is TreeNode selectedNode && selectedNode.RightNode != null)
            {
                FileOperationHandler.OpenFileLocation(selectedNode.RightNode.FullPath);
            }
        }
        private void ContextMenu_CopyPathnames_Click(object sender, RoutedEventArgs e)
        {
            bool isLeft = sender is MenuItem { Parent: ContextMenu contextMenu } && contextMenu == FindResource("LeftContextMenu");

            var paths = ResultListView.SelectedItems
                .Cast<TreeNode>()
                .Select(node => isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToList();

            if (paths.Count > 0)
            {
                Clipboard.SetText(string.Join("\n", paths));
            }
            else
            {
                MessageBox.Show(Properties.Resources.NoValidPathsSelected, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ContextMenu_CopyFilenames_Click(object sender, RoutedEventArgs e)
        {
            bool isLeft = sender is MenuItem { Parent: ContextMenu contextMenu } && contextMenu == FindResource("LeftContextMenu");
            var filenames = ResultListView.SelectedItems
                .Cast<TreeNode>()
                .Select(node => isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(Path.GetFileName)
                .ToList();

            if (filenames.Count > 0)
            {
                Clipboard.SetText(string.Join("\n", filenames));
            }
            else
            {
                MessageBox.Show(Properties.Resources.NoValidFilesSelected, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region ViewAndCompare
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
                        _treeNodeManager.RemoveChildren(clickedItem);
                    }
                    else
                    {
                        clickedItem.IsExpanded = true;
                        _treeNodeManager.LoadChildren(clickedItem, _cachedComparisonResults, ViewModel.LeftPath?.Trim() ?? string.Empty, ViewModel.RightPath?.Trim() ?? string.Empty);
                    }
                }
                else
                {
                    var config = await _configService.LoadConfigurationAsync();
                    if (config.Options.Compare.DoubleClick == DoubleClickActions.View)
                    {
                        ViewFileAsync(clickedItem);
                    }
                    else
                    {
                        CompareFileAsync(clickedItem);
                    }
                }
            }
        }
        private async Task PopulateMenusForNodeAsync(TreeNode node)
        {
            // TODO, need to inject it
            ToolFactory _toolFactory = new ToolFactory(_configService);
            ToolConfigService _toolConfigService = new();

            try
            {
                var config = await _configService.LoadConfigurationAsync();
                var filePath = node.LeftNode?.FullPath ?? node.RightNode?.FullPath;
                if (filePath == null) return;

                var fileType = _toolFactory.DetermineFileType(config, filePath);
                if (fileType == null) return;

                var fileTypeMapping = config.Options.FileTypeMapping.GetValueOrDefault(fileType);
                if (fileTypeMapping == null) return;

                if (FindResource("LeftContextMenu") is not ContextMenu leftContextMenu ||
                    FindResource("RightContextMenu") is not ContextMenu rightContextMenu)
                    return;

                var leftCompareWithMenu = FindMenuByName(leftContextMenu, "LeftCompareWithMenu");
                var rightCompareWithMenu = FindMenuByName(rightContextMenu, "RightCompareWithMenu");
                var leftViewWithMenu = FindMenuByName(leftContextMenu, "LeftViewWithMenu");
                var rightViewWithMenu = FindMenuByName(rightContextMenu, "RightViewWithMenu");

                if (leftCompareWithMenu == null || rightCompareWithMenu == null || leftViewWithMenu == null || rightViewWithMenu == null) return;

                var diffToolNames = fileTypeMapping.PreferredTools.Diff;
                var viewToolNames = fileTypeMapping.PreferredTools.View;

                var diffTools = config.Options.Tools
                                      .Where(t => diffToolNames.Contains(t.Key))
                                      .Select(t => t.Value);

                var viewTools = config.Options.Tools
                                      .Where(t => viewToolNames.Contains(t.Key))
                                      .Select(t => t.Value);

                PopulateToolMenu(leftCompareWithMenu, diffTools, ContextMenu_CompareWithViewer_Click);
                PopulateToolMenu(rightCompareWithMenu, diffTools, ContextMenu_CompareWithViewer_Click);
                PopulateToolMenu(leftViewWithMenu, viewTools, ContextMenu_ViewWithViewer_Click);
                PopulateToolMenu(rightViewWithMenu, viewTools, ContextMenu_ViewWithViewer_Click);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to populate dynamic menus: {Message}", ex.Message);
                MessageBox.Show(string.Format(Properties.Resources.ErrorLoadingTools, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static MenuItem? FindMenuByName(ContextMenu menu, string name) =>
            menu.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Name == name);
        private void PopulateToolMenu(MenuItem parentMenu, IEnumerable<ToolOptions> tools, RoutedEventHandler clickHandler)
        {
            parentMenu.Items.Clear();
            foreach (var tool in tools)
            {
                var item = new MenuItem
                {
                    Header = tool.Name,
                    Tag = tool
                };
                item.Click += clickHandler;
                parentMenu.Items.Add(item);
            }
        }
        private async void ViewFileAsync(TreeNode selectedItem)
        {
            await LaunchToolAsync(selectedItem, ToolOperationTypes.View);
        }
        private void ContextMenu_ViewWithViewer_Click(object sender, RoutedEventArgs e)
        {
            ProcessToolAction(sender, async (selectedItem, tool) =>
            {
                await LaunchToolAsync(selectedItem, ToolOperationTypes.View, tool);
            });
        }
        private async void CompareFileAsync(TreeNode selectedItem)
        {
            await LaunchToolAsync(selectedItem, ToolOperationTypes.Diff);
        }
        private void ContextMenu_CompareWithViewer_Click(object sender, RoutedEventArgs e)
        {
            ProcessToolAction(sender, async (selectedItem, tool) =>
            {
                await LaunchToolAsync(selectedItem, ToolOperationTypes.Diff, tool);
            });
        }
        private async Task LaunchToolAsync(TreeNode selectedItem, ToolOperationTypes operation, ToolOptions? toolConfig = null)
        {
            TabItem? tab = await _toolHandler.LaunchToolAsync(selectedItem, operation, toolConfig);
            if (tab != null)
            {
                if (WpfTreeHelperExtensions.FindParent<TabControl>(this) is not TabControl tabControl)
                {
                    MessageBox.Show(Properties.Resources.CannotLocateParentTabControl, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                tabControl.Items.Add(tab);
                tabControl.SelectedItem = tab;
            }
        }
        #endregion

        void ITool.LoadFile(string filePath)
        {
            throw new NotImplementedException();
        }

        UserControl ITool.GetControl()
        {
            return this;
        }
    }
}