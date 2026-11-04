using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.FileOperation;
using dCompare.Messages;
using dCompare.Tools;
using CommunityToolkit.Mvvm.Messaging;
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

    public partial class CompareControl : UserControl, INotifyPropertyChanged, IRefreshable
    {
        private readonly IConfigurationService _configService;
        private readonly ILogger<CompareControl> _logger;
        private readonly IFileManager _fileManager;
        private readonly ToolHandler _toolHandler;

        private DirectoryComparer _comparer = new();
        private List<DirectoryComparer.ComparisonItem> _cachedComparisonResults = null!;
        private ObservableCollection<TreeNode> _treeNodes = new();
        private ListSortDirection _lastSortDirection;
        private GridViewColumnHeader _lastHeaderClicked = null!;
        private Predicate<object>? _currentFilter = null;   // null for non-filter, means all data
        private CancellationTokenSource _cancellationTokenSource = null!;

        private readonly TreeNodeManager _treeNodeManager;
        private readonly FileOperationHandler _fileOperationHandler;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public string FullPathsTooltip { get; private set; } = null!;

        public CompareControlViewModel ViewModel { get; }

        public CompareControl(IConfigurationService configService, IFileManager fileManager, ILogger<CompareControl> logger, IUserInteractionService userInteractionService)
        {
            InitializeComponent();

            _logger = logger;
            _fileManager = fileManager;
            _configService = configService;
            _toolHandler = new ToolHandler();
            _treeNodeManager = new TreeNodeManager(_treeNodes);
            _fileOperationHandler = new FileOperationHandler(_fileManager, _configService, userInteractionService);

            // Initialize ViewModel
            ViewModel = new CompareControlViewModel();
            DataContext = ViewModel;

            // Subscribe to ViewModel Path changes to update tab header
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            ResultListView.ItemsSource = _treeNodes;

            SetFilter(null);

            // Settings | Save changes which algorithms exist and how the scan runs, so the open tab has to hear
            // about it. Loaded was the old trigger and it fires on every tab switch (the TabControl hosts only
            // the selected tab), which re-read the file and reset the algorithm the user had just chosen.
            WeakReferenceMessenger.Default.Register<OptionsSavedMessage>(this, (_, _) => _ = InitializeAsync());
            _ = InitializeAsync();
        }

        public CompareControl(IConfigurationService configService, IFileManager fileManager, ILogger<CompareControl> logger, IUserInteractionService userInteractionService, string leftPath, string rightPath)
            : this(configService, fileManager, logger, userInteractionService)
        {
            // TODO This is for archive file only, may consider using other solution
            ViewModel.LeftPath = leftPath;
            ViewModel.RightPath = rightPath;
            this.Refresh();
        }
        /// <summary>
        /// Reads dCompare.json once and configures the tab from it: the algorithm dropdown and its default, and the
        /// comparer itself (its scan settings and per-algorithm hash ratio come from the same file). A corrupt or
        /// unreadable config still leaves the tab able to compare, on the built-in seed.
        /// </summary>
        private async Task InitializeAsync()
        {
            AppConfiguration config;
            try
            {
                config = await _configService.LoadConfigurationAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load configuration: {Message}", ex.Message);
                MessageBox.Show(string.Format(Properties.Resources.ErrorLoadingConfiguration, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                config = new AppConfiguration();
            }

            ViewModel.LoadAlgorithms(config.Options.Compare.Algorithms);
            _comparer = new DirectoryComparer(config);
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
                baseHeader = Properties.Resources.Untitled;
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

        // The file-dialog folder picker trick: a real file by this name would be taken as "the user chose the
        // folder it sits in", so the two uses (seed the box, recognise the choice) must stay in step.
        private const string FolderSelectionSentinel = "Folder Selection.";

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = Properties.Resources.SelectFolderOrArchiveTitle,
                Filter = Properties.Resources.ArchiveFileFilter,
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = FolderSelectionSentinel
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                // If the user selected a directory, the file name will be the sentinel above.
                if (selectedPath.Contains(FolderSelectionSentinel, StringComparison.Ordinal))
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
            viewModel.BeginCompare();
            try
            {
                List<DirectoryComparer.ComparisonItem> comparisonResults = await Task.Run(() =>
                    _comparer.CompareAsync(leftDir, rightDir, _cancellationTokenSource.Token,
                        progress => Dispatcher.InvokeAsync(() => viewModel.ReportProgress(progress)),
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

        // Property to sort by, in the order the columns are declared in CompareControl.xaml: left name, left size,
        // left date, status, right name, right size, right date. Keyed on the column index because the header text
        // is localised, and switching on it meant that in zh-CN and zh-TW every header click sorted by left name.
        private static readonly string[] ColumnSortProperties =
        {
            "LeftDisplayName", "LeftSize", "LeftDate", "StatusSymbol", "RightDisplayName", "RightSize", "RightDate"
        };

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader headerClicked || headerClicked.Column is null) return;

            int columnIndex = ((GridView)ResultListView.View).Columns.IndexOf(headerClicked.Column);
            string propertyName = columnIndex >= 0 && columnIndex < ColumnSortProperties.Length
                ? ColumnSortProperties[columnIndex]
                : ColumnSortProperties[0];

            ListSortDirection direction = _lastHeaderClicked == headerClicked && _lastSortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            ICollectionView view = CollectionViewSource.GetDefaultView(_treeNodes);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(propertyName, direction));

            _lastHeaderClicked = headerClicked;
            _lastSortDirection = direction;
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
                {
                    string relativePath = _treeNodeManager.GetRelativePath(item, leftDirRoot, rightDirRoot);
                    // The separator matters: without it, refreshing "Data" also purged every cached row under "Database".
                    return relativePath.Equals(nodeToRefresh.RelativePath, StringComparison.OrdinalIgnoreCase)
                        || relativePath.StartsWith(nodeToRefresh.RelativePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                });

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
        // Toolbar/menu entry points (MainWindowViewModel): no XAML binds these, so they take no event args.
        public void ExpandAll()
        {
            if (_cachedComparisonResults == null) return;

            string leftDir = ViewModel.LeftPath?.Trim() ?? string.Empty;
            string rightDir = ViewModel.RightPath?.Trim() ?? string.Empty;

            _treeNodes.Clear();

            List<TreeNode> allNodes = _treeNodeManager.CreateFlatTreeStructure(_cachedComparisonResults, leftDir, rightDir);

            foreach (TreeNode node in allNodes)
            {
                node.IsExpanded = node.IsDirectory;
                _treeNodes.Add(node);
            }
            ApplyFilter();
        }

        public void CollapseAll()
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

        public void ShowAll() => SetFilter(null);
        public void ShowDifferent() =>
            SetFilter(node => node.Status == ComparisonStatus.Modified ||
                            node.Status == ComparisonStatus.Conflict ||
                            node.Status == ComparisonStatus.LeftOnly ||
                            node.Status == ComparisonStatus.RightOnly);
        public void ShowIdentical() => SetFilter(node => node.Status == ComparisonStatus.Identical);
        public void ShowLeftUnique() => SetFilter(node => node.Status == ComparisonStatus.LeftOnly);
        public void ShowRightUnique() => SetFilter(node => node.Status == ComparisonStatus.RightOnly);

        // Same entry point as the context menu, so the batch confirmation and the empty-selection
        // warning stay in one place (ProcessFileOperationsAsync / FileOperationHandler).
        public void CopyToLeft() => ContextMenu_CopyToLeft_Click(this, new RoutedEventArgs());
        public void CopyToRight() => ContextMenu_CopyToRight_Click(this, new RoutedEventArgs());

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

            // Rows read from inside an archive cannot be written back: their path is fabricated, so a delete
            // there was a no-op the transfer panel still reported as completed.
            if (FileOperationHandler.ContainsArchiveEntry(selectedNodes))
            {
                MessageBox.Show(Properties.Resources.ArchiveEntryOperationNotSupported,
                    Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HashSet<string> previouslyExpandedPaths = new(
                _treeNodes.Where(n => n.IsDirectory && n.IsExpanded)
                          .Select(n => n.RelativePath)
            );

            try
            {
                // One call for the whole selection: the handler confirms copy/move/delete once per batch,
                // on the UI thread, instead of prompting for every selected node.
                switch (operation)
                {
                    case FileOperationType.CopyToLeft:
                        await _fileOperationHandler.CopyToLeft(selectedNodes, destDir);
                        break;
                    case FileOperationType.CopyToRight:
                        await _fileOperationHandler.CopyToRight(selectedNodes, destDir);
                        break;
                    case FileOperationType.MoveToLeft:
                        await _fileOperationHandler.MoveToLeft(selectedNodes, destDir);
                        break;
                    case FileOperationType.MoveToRight:
                        await _fileOperationHandler.MoveToRight(selectedNodes, destDir);
                        break;
                    case FileOperationType.DeleteLeft:
                        var leftItems = _fileOperationHandler.GetItemsForDeletion(selectedNodes, true);
                        if (leftItems.Count > 0) await _fileOperationHandler.DeleteAsync(leftItems);
                        break;
                    case FileOperationType.DeleteRight:
                        var rightItems = _fileOperationHandler.GetItemsForDeletion(selectedNodes, false);
                        if (rightItems.Count > 0) await _fileOperationHandler.DeleteAsync(rightItems);
                        break;
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

            // Rename does not go through FileOperationHandler.GetItemsFor*, so it needs the archive guard the
            // other operations get there: the path is fabricated, and without this the user typed a new name
            // into the dialog only to be told "file not found" about a row they can see.
            if (selectedItem.IsArchiveEntry)
            {
                MessageBox.Show(Properties.Resources.ArchiveEntryOperationNotSupported,
                    Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
        private void ContextMenu_OpenLeftLocation_Click(object sender, RoutedEventArgs e) => OpenLocation(sender, isLeft: true);
        private void ContextMenu_OpenRightLocation_Click(object sender, RoutedEventArgs e) => OpenLocation(sender, isLeft: false);

        private void OpenLocation(object sender, bool isLeft)
        {
            if (ResultListView.SelectedItem is not TreeNode selectedNode) return;
            var node = isLeft ? selectedNode.LeftNode : selectedNode.RightNode;
            if (node is null) return;

            // A row read from inside an archive has a fabricated path: Explorer can only be sent somewhere that
            // does not exist, so it answered "path does not exist" for a row sitting in front of the user.
            if (node.IsArchiveEntry)
            {
                MessageBox.Show(Properties.Resources.ArchiveEntryOperationNotSupported,
                    Properties.Resources.Warning, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FileOperationHandler.OpenFileLocation(node.FullPath);
        }
        private void ContextMenu_CopyPathnames_Click(object sender, RoutedEventArgs e) => CopySelectedPaths(sender, fileNameOnly: false);
        private void ContextMenu_CopyFilenames_Click(object sender, RoutedEventArgs e) => CopySelectedPaths(sender, fileNameOnly: true);

        private void CopySelectedPaths(object sender, bool fileNameOnly)
        {
            bool isLeft = sender is MenuItem { Parent: ContextMenu contextMenu } && contextMenu == FindResource("LeftContextMenu");

            var values = ResultListView.SelectedItems
                .Cast<TreeNode>()
                .Select(node => isLeft ? node.LeftNode?.FullPath : node.RightNode?.FullPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => fileNameOnly ? Path.GetFileName(path) : path)
                .ToList();

            if (values.Count == 0)
            {
                MessageBox.Show(fileNameOnly ? Properties.Resources.NoValidFilesSelected : Properties.Resources.NoValidPathsSelected,
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Clipboard.SetText(string.Join("\n", values));
            }
            catch (Exception ex)
            {
                // The clipboard is a shared resource: another process holding it makes SetText throw
                // (CLIPBRD_E_CANT_OPEN). Unhandled, it took the window down from a read-only menu item.
                _logger.LogWarning(ex, "Could not copy {Count} value(s) to the clipboard.", values.Count);
                MessageBox.Show(string.Format(Properties.Resources.ErrorCopyingToClipboard, ex.Message),
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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
            ILogger<ToolFactory> logger = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<ILogger<ToolFactory>>();
            ToolFactory _toolFactory = new(_configService, logger);
            ToolConfigService _toolConfigService = new();

            if (FindResource("LeftContextMenu") is not ContextMenu leftContextMenu ||
                FindResource("RightContextMenu") is not ContextMenu rightContextMenu)
                return;

            MenuItem? leftCompareWithMenu = FindMenuByName(leftContextMenu, "LeftCompareWithMenu");
            MenuItem? rightCompareWithMenu = FindMenuByName(rightContextMenu, "RightCompareWithMenu");
            MenuItem? leftViewWithMenu = FindMenuByName(leftContextMenu, "LeftViewWithMenu");
            MenuItem? rightViewWithMenu = FindMenuByName(rightContextMenu, "RightViewWithMenu");

            // Clear previous items to avoid showing stale menus
            leftCompareWithMenu?.Items.Clear();
            rightCompareWithMenu?.Items.Clear();
            leftViewWithMenu?.Items.Clear();
            rightViewWithMenu?.Items.Clear();

            try
            {
                var config = await _configService.LoadConfigurationAsync();
                string filePath = node.LeftNode?.FullPath ?? node.RightNode?.FullPath;
                if (filePath == null) return;

                string fileType = _toolFactory.DetermineFileType(config, filePath);
                if (fileType == null) return;

                var fileTypeMapping = config.Options.FileTypeMapping.GetValueOrDefault(fileType);
                if (fileTypeMapping == null) return;

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

    }
}