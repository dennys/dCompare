using dCompare.FileOperation;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace dCompare.Compare
{
    public static class LogicalTreeHelperExtensions
    {
        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                child = LogicalTreeHelper.GetParent(child);
                if (child is T parent)
                {
                    return parent;
                }
            }
            return null;
        }
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

        private Operation _fileOperationWindow;

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
            TabItem tabItem = LogicalTreeHelperExtensions.FindParent<TabItem>(this);
            if (tabItem != null)
            {
                if (IsComparing)
                {
                    tabItem.Header = "(Comparing...) " + baseHeader;
                    //tabItem.Header = new StackPanel
                    //{
                    //    Orientation = Orientation.Horizontal,
                    //    Children =
                    //    {
                    //        new System.Windows.Shapes.Ellipse { Style = (Style)Resources["SpinnerStyle"] },
                    //        new TextBlock { Text = baseHeader, Margin = new Thickness(5, 0, 0, 0) }
                    //    }
                    //};
                }
                else
                {
                    tabItem.Header = baseHeader;
                }
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

        private void BrowseLeft_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            if (folderDialog.ShowDialog() == true)
            {
                LeftPathBox.Text = folderDialog.FolderName;
            }
        }

        private void BrowseRight_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select Folder",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            if (folderDialog.ShowDialog() == true)
            {
                RightPathBox.Text = folderDialog.FolderName;
            }
        }

        private async void Compare_Click(object sender, RoutedEventArgs e)
        {
            if (IsComparing)
            {
                MessageBox.Show("Comparison is already in progress.");
                return;
            }

            _treeNodes.Clear();
            _cachedComparisonResults = null;
            _cancellationTokenSource = new CancellationTokenSource();

            string leftDir = LeftPathBox.Text.Trim();
            string rightDir = RightPathBox.Text.Trim();

            if (!Directory.Exists(leftDir) || !Directory.Exists(rightDir))
            {
                MessageBox.Show("請輸入有效的左右資料夾路徑");
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
                List<TreeNode>? flatNodes = BuildFlatTreeStructure(comparisonResults, leftDir, rightDir, maxDepth: 0);

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
                MessageBox.Show($"比較過程中發生錯誤: {ex.Message}");
            }
            finally
            {
                IsComparing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelCompare_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        private List<TreeNode> BuildFlatTreeStructure(List<DirectoryComparer.ComparisonItem> items, string leftRoot, string rightRoot, int maxDepth = int.MaxValue)
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
            while (obj != null && !(obj is ListViewItem))
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
                    // Handle file double-click
                    string leftPath = clickedItem.LeftNode?.FullPath;
                    string rightPath = clickedItem.RightNode?.FullPath;

                    if (string.IsNullOrEmpty(leftPath) && string.IsNullOrEmpty(rightPath))
                    {
                        MessageBox.Show("No valid file paths to compare.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var comparisonHandler = new FileComparisonHandler();
                    FileType fileType = comparisonHandler.DetermineFileType(leftPath ?? rightPath);

                    if (fileType == FileType.Text)
                    {
                        try
                        {
                            // Ensure both files exist; use a placeholder for missing files
                            leftPath = leftPath ?? CreateTemporaryEmptyFile();
                            rightPath = rightPath ?? CreateTemporaryEmptyFile();

                            var comparisonResult = await comparisonHandler.CompareFilesAsync(leftPath, rightPath, fileType);
                            OpenFileComparisonTab(leftPath, rightPath, comparisonResult);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error comparing files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else if (fileType == FileType.Image)
                    {
                        MessageBox.Show("Image comparison is not yet supported.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Unsupported file type for comparison.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Add this new method to create a temporary empty file for missing files
        private string CreateTemporaryEmptyFile()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"dCompare_Empty_{Guid.NewGuid()}.txt");
            File.WriteAllText(tempPath, string.Empty);
            return tempPath;
        }

        // Add this new method to open a new tab with the file comparison
        private void OpenFileComparisonTab(string leftPath, string rightPath, object comparisonResult)
        {
            TabItem parentTab = LogicalTreeHelperExtensions.FindParent<TabItem>(this);
            TabControl tabControl = LogicalTreeHelperExtensions.FindParent<TabControl>(parentTab);
            if (tabControl == null)
            {
                MessageBox.Show("Cannot find parent TabControl.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string tabHeader = $"Compare: {Path.GetFileName(leftPath)} - {Path.GetFileName(rightPath)}";
            var newTab = new TabItem
            {
                Header = tabHeader,
                Content = new UcFileCompare()
            };

            ((UcFileCompare)newTab.Content).SetComparisonResult(leftPath, rightPath, comparisonResult);
            tabControl.Items.Add(newTab);
            tabControl.SelectedItem = newTab;
        }

        // Confirm that RemoveChildren and LoadChildren are included (from original UcCompare.xaml.cs)
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

            List<TreeNode> childNodes = BuildFlatTreeStructure(childItems, leftDir, rightDir, parentNode.Depth + 1);
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

        private void LoadChildren2(TreeNode parentNode)
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

            List<TreeNode> childNodes = BuildFlatTreeStructure(childItems, leftDir, rightDir, parentNode.Depth + 1);
            int insertIndex = _treeNodes.IndexOf(parentNode) + 1;

            foreach (TreeNode child in childNodes)
            {
                child.IsExpanded = false;
                _treeNodes.Insert(insertIndex++, child);
            }
        }

        private void RemoveChildren2(TreeNode parentNode)
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

            List<TreeNode> allNodes = BuildFlatTreeStructure(_cachedComparisonResults, leftDir, rightDir);

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

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = null;
            ApplyFilter();
        }

        private void ShowDifferent_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && (node.Status == ComparisonStatus.Modified || node.Status == ComparisonStatus.Conflict);
            ApplyFilter();
        }

        private void ShowIdentical_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && node.Status == ComparisonStatus.Identical;
            ApplyFilter();
        }

        private void ShowLeftUnique_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && node.Status == ComparisonStatus.LeftOnly;
            ApplyFilter();
        }

        private void ShowRightUnique_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && node.Status == ComparisonStatus.RightOnly;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            ICollectionView? view = CollectionViewSource.GetDefaultView(_treeNodes);
            view.Filter = _currentFilter;
        }

        private void CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedFiles(toLeft: true);
        }

        private void CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            CopySelectedFiles(toLeft: false);
        }

        private void CopySelectedFiles(bool toLeft)
        {
            List<TreeNode> selectedItems = ResultListView.SelectedItems.Cast<TreeNode>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("請選擇至少一個項目進行同步");
                return;
            }

            string targetRoot = toLeft ? LeftPathBox.Text.Trim() : RightPathBox.Text.Trim();
            string sourceRoot = toLeft ? RightPathBox.Text.Trim() : LeftPathBox.Text.Trim();

            int successCount = 0;
            int errorCount = 0;

            foreach (TreeNode? node in selectedItems)
            {
                if (node.IsDirectory) continue;

                try
                {
                    string sourcePath = Path.Combine(sourceRoot, node.RelativePath);
                    string targetPath = Path.Combine(targetRoot, node.RelativePath);

                    string? targetDir = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, targetPath, true);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    MessageBox.Show($"複製檔案失敗: {node.RelativePath}\n{ex.Message}");
                }
            }

            MessageBox.Show($"同步完成\n成功: {successCount} 個檔案\n失敗: {errorCount} 個檔案");
            Compare_Click(null, null);
        }

        private void ListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevents selection on right-click
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

        private void ContextMenu_CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem && !(sender is MenuItem)) return;

            if (sender is MenuItem menuItem && menuItem.Tag is TreeNode selectedItem)
            {
                FileOperations.CopySelectedItemTo(selectedItem, true, LeftPathBox.Text.Trim(), RightPathBox.Text.Trim());
                Compare_Click(null, null);
            }
        }

        private void ContextMenu_CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            OpenFileOperations_Click();
            CopyFile_Click();
            //if (sender is MenuItem menuItem && menuItem.Tag is TreeNode selectedItem)
            //{
            //    FileOperations.CopySelectedItemTo(selectedItem, false, LeftPathBox.Text.Trim(), RightPathBox.Text.Trim());
            //    Compare_Click(null, null);
            //}
        }

        private void ContextMenu_OpenLeftLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TreeNode selectedItem && selectedItem.LeftNode != null)
            {
                FileOperations.OpenFileLocation(selectedItem.LeftNode.FullPath);
            }
            else
            {
                MessageBox.Show("左側路徑不存在");
            }
        }

        private void ContextMenu_OpenRightLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TreeNode selectedItem && selectedItem.RightNode != null)
            {
                FileOperations.OpenFileLocation(selectedItem.RightNode.FullPath);
            }
            else
            {
                MessageBox.Show("右側路徑不存在");
            }
        }

        private string GetRelativePath(DirectoryComparer.ComparisonItem item, string leftRoot, string rightRoot)
        {
            string path = "";
            if (item.Left != null)
            {
                path = item.Left.FullPath.Substring(leftRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            else if (item.Right != null)
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

        #region
        private void OpenFileOperations_Click()
        {
            if (_fileOperationWindow == null || !_fileOperationWindow.IsLoaded)
            {
                _fileOperationWindow = new Operation();
                _fileOperationWindow.OnTransferStatusChanged += FileOperation_OnTransferStatusChanged;
                _fileOperationWindow.Closed += (s, args) => _fileOperationWindow = null;
                _fileOperationWindow.Show();
            }
            else
            {
                _fileOperationWindow.Activate();
            }
        }

        private void CopyFile_Click()
        {
            if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
            {
                // Example paths; replace with actual file selection logic
                _fileOperationWindow.Copy("d:\\a.pptx", "d:\\a2.pptx", false);
                //StatusText.Text = "Copy operation queued.";
            }
            else
            {
                //StatusText.Text = "Please open File Operations window first.";
            }
        }

        private void MoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (_fileOperationWindow != null && _fileOperationWindow.IsLoaded)
            {
                // Example paths; replace with actual file selection logic
                _fileOperationWindow.Move("source.txt", "target.txt", false);
                //StatusText.Text = "Move operation queued.";
            }
            else
            {
                //StatusText.Text = "Please open File Operations window first.";
            }
        }

        private void FileOperation_OnTransferStatusChanged(string jobId, bool success, string message)
        {
            string a = $"Job {jobId}: {message}";
            Console.Write(a);
            MessageBox.Show(a);
            Dispatcher.Invoke(() =>
            {
                //StatusText.Text = $"Job {jobId}: {message}";
            });
        }
        #endregion
    }
}