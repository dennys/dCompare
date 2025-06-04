using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using dCompare.Compare;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Windows.Media;
using System.Windows.Input;

namespace dCompare
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
    public partial class UcCompare : UserControl
    {
        private ObservableCollection<TreeNode> _treeNodes = new();
        private DirectoryComparer _comparer = new DirectoryComparer();
        private ListSortDirection _lastSortDirection;
        private GridViewColumnHeader _lastHeaderClicked;
        private Predicate<object> _currentFilter = null; // Store current filter

        public UcCompare()
        {
            InitializeComponent();
            ResultListView.ItemsSource = _treeNodes;
            ResultListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeaderClickedHandler));
        }

        public void ChangeTabHeader(string newHeader)
        {
            TabItem tabItem = LogicalTreeHelperExtensions.FindParent<TabItem>(this);
            if (tabItem != null)
            {
                tabItem.Header = newHeader;
            }
        }

        private void BrowseLeft_Click(object sender, RoutedEventArgs e)
        {
            // Note: Folder selection dialog implementation would need to be handled
            // without Microsoft.Win32. Consider using a different approach or library
            MessageBox.Show("請選擇左側資料夾");
        }

        private void BrowseRight_Click(object sender, RoutedEventArgs e)
        {
            // Note: Folder selection dialog implementation would need to be handled
            // without Microsoft.Win32. Consider using a different approach or library
            MessageBox.Show("請選擇右側資料夾");
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            _treeNodes.Clear();

            string leftDir = LeftPathBox.Text.Trim();
            string rightDir = RightPathBox.Text.Trim();

            if (!Directory.Exists(leftDir) || !Directory.Exists(rightDir))
            {
                MessageBox.Show("請輸入有效的左右資料夾路徑");
                return;
            }

            try
            {
                List<DirectoryComparer.ComparisonItem>? comparisonResults = _comparer.Compare(leftDir, rightDir);
                List<TreeNode>? flatNodes = BuildFlatTreeStructure(comparisonResults, leftDir, rightDir);

                foreach (TreeNode? node in flatNodes)
                {
                    _treeNodes.Add(node);
                }

                ApplyCurrentFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"比較過程中發生錯誤: {ex.Message}");
            }
            ChangeTabHeader(LeftPathBox.Text + " - " + RightPathBox.Text);
            CollapseAll_Click(null, null);
        }

        private List<TreeNode> BuildFlatTreeStructure(List<DirectoryComparer.ComparisonItem> items, string leftRoot, string rightRoot)
        {
            List<TreeNode> flatNodes = new List<TreeNode>();

            foreach (DirectoryComparer.ComparisonItem? item in items)
            {
                string? relativePath = GetRelativePath(item, leftRoot, rightRoot);
                if (string.IsNullOrEmpty(relativePath)) continue;

                TreeNode node = new TreeNode
                {
                    Name = item.Left?.Name ?? item.Right?.Name ?? Path.GetFileName(relativePath),
                    RelativePath = relativePath,
                    IsDirectory = item.Left?.IsDirectory ?? item.Right?.IsDirectory ?? false,
                    Depth = relativePath.Count(c => c == Path.DirectorySeparatorChar)
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
        private void ResultListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DependencyObject obj = (DependencyObject)e.OriginalSource;
            while (obj != null && !(obj is ListViewItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }

            if (obj is ListViewItem item)
            {
                if (item.Content is TreeNode clickedItem && clickedItem.IsDirectory)
                {
                    clickedItem.IsExpanded = !clickedItem.IsExpanded;
                    ApplyCurrentFilter();
                }
            }
        }

        private bool IsNodeVisible(TreeNode node)
        {
            // Always show root level items
            if (node.Depth == 0) return true;

            // For child items, check if all parent directories are expanded
            string currentPath = "";
            string[] pathParts = node.RelativePath.Split(Path.DirectorySeparatorChar);

            for (int i = 0; i < pathParts.Length - 1; i++) // Exclude the node itself
            {
                if (i == 0)
                    currentPath = pathParts[i];
                else
                    currentPath = Path.Combine(currentPath, pathParts[i]);

                TreeNode? parentNode = _treeNodes.FirstOrDefault(n => n.IsDirectory &&
                    n.RelativePath.Equals(currentPath, StringComparison.OrdinalIgnoreCase));

                if (parentNode != null && !parentNode.IsExpanded)
                    return false;
            }

            return true;
        }

        private void ApplyCollapseFilter()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(_treeNodes);
            view.Filter = obj => obj is TreeNode node && node.Depth == 0;
        }

        private void ApplyCurrentFilter()
        {
            ICollectionView? view = CollectionViewSource.GetDefaultView(_treeNodes);

            view.Filter = obj =>
            {
                if (obj is not TreeNode node) return false;

                // First check visibility based on expansion state
                if (!IsNodeVisible(node)) return false;

                // Then apply any additional filter
                if (_currentFilter != null)
                    return _currentFilter(obj);

                return true;
            };
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (TreeNode? node in _treeNodes)
            {
                if (node.IsDirectory) node.IsExpanded = true;
            }
            ApplyCurrentFilter();
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (TreeNode? node in _treeNodes)
            {
                if (node.IsDirectory) node.IsExpanded = false;
            }
            ApplyCurrentFilter();
        }

        private void ShowAll_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = null;
            ApplyCurrentFilter();
        }

        private void ShowDifferent_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && (node.Status == ComparisonStatus.Modified || node.Status == ComparisonStatus.Conflict);
            ApplyCurrentFilter();
        }

        private void ShowIdentical_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && node.Status == ComparisonStatus.Identical;
            ApplyCurrentFilter();
        }

        private void ShowLeftUnique_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && node.Status == ComparisonStatus.LeftOnly;
            ApplyCurrentFilter();
        }

        private void ShowRightUnique_Click(object sender, RoutedEventArgs e)
        {
            _currentFilter = obj => obj is TreeNode node && node.Status == ComparisonStatus.RightOnly;
            ApplyCurrentFilter();
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
            if (sender is MenuItem menuItem && menuItem.Tag is TreeNode selectedItem)
            {
                FileOperations.CopySelectedItemTo(selectedItem, true, LeftPathBox.Text.Trim(), RightPathBox.Text.Trim());
                Compare_Click(null, null);
            }
        }

        private void ContextMenu_CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TreeNode selectedItem)
            {
                FileOperations.CopySelectedItemTo(selectedItem, false, LeftPathBox.Text.Trim(), RightPathBox.Text.Trim());
                Compare_Click(null, null);
            }
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
                if (!item.Left.IsDirectory)
                {
                    node.LeftSize = FileSystemNode.FormatSize(item.Left.Size);
                }
                node.LeftDate = item.Left.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }

            if (item.Right != null)
            {
                node.RightName = item.Right.Name;
                if (!item.Right.IsDirectory)
                {
                    node.RightSize = FileSystemNode.FormatSize(item.Right.Size);
                }
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
    }
}