using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;using System.Windows.Media;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

    /// <summary>
    /// ucCompare.xaml 的互動邏輯
    /// </summary>
    public partial class UcCompare : UserControl
    {
        private ObservableCollection<TreeNode> _treeNodes = new ObservableCollection<TreeNode>();
        private DirectoryComparer _comparer = new DirectoryComparer();
        public UcCompare()
        {
            InitializeComponent();
            ResultTreeView.ItemsSource = _treeNodes;
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
            //var dialog = new System.Windows.Forms.FolderBrowserDialog();
            //if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    LeftPathBox.Text = dialog.SelectedPath;
            //}
        }

        private void BrowseRight_Click(object sender, RoutedEventArgs e)
        {
            //var dialog = new System.Windows.Forms.FolderBrowserDialog();
            //if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            //{
            //    RightPathBox.Text = dialog.SelectedPath;
            //}
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
                var comparisonResults = _comparer.Compare(leftDir, rightDir);
                var rootNodes = BuildTreeStructure(comparisonResults, leftDir, rightDir);

                foreach (var node in rootNodes)
                {
                    _treeNodes.Add(node);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"比較過程中發生錯誤: {ex.Message}");
            }
            ChangeTabHeader(LeftPathBox.Text + " - " + RightPathBox.Text);
        }

        private List<TreeNode> BuildTreeStructure(List<DirectoryComparer.ComparisonItem> items, string leftRoot, string rightRoot)
        {
            var nodeDict = new Dictionary<string, TreeNode>();
            var rootNodes = new List<TreeNode>();

            foreach (var item in items)
            {
                var relativePath = GetRelativePath(item, leftRoot, rightRoot);
                if (string.IsNullOrEmpty(relativePath)) continue;

                var pathParts = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                TreeNode currentParent = null;
                string currentPath = "";

                for (int i = 0; i < pathParts.Length; i++)
                {
                    if (i > 0) currentPath += Path.DirectorySeparatorChar;
                    currentPath += pathParts[i];

                    if (!nodeDict.ContainsKey(currentPath))
                    {
                        var node = new TreeNode
                        {
                            Name = pathParts[i],
                            RelativePath = currentPath,
                            IsDirectory = i < pathParts.Length - 1 || (item.Left?.IsDirectory ?? item.Right?.IsDirectory ?? false),
                            Depth = i
                        };

                        // 如果是最後一層，設定詳細資訊
                        if (i == pathParts.Length - 1)
                        {
                            SetNodeDetails(node, item, leftRoot, rightRoot);
                        }
                        else
                        {
                            // 中間層目錄，需要查找對應的比較項目來設定詳細資訊
                            var dirItem = items.FirstOrDefault(x =>
                                GetRelativePath(x, leftRoot, rightRoot) == currentPath);
                            if (dirItem != null)
                            {
                                SetNodeDetails(node, dirItem, leftRoot, rightRoot);
                            }
                            else
                            {
                                // 預設為目錄存在於兩邊
                                node.LeftName = node.RightName = pathParts[i];
                                node.StatusSymbol = "=";
                                node.Status = ComparisonStatus.Identical;
                            }
                        }

                        nodeDict[currentPath] = node;

                        if (currentParent == null)
                        {
                            rootNodes.Add(node);
                        }
                        else
                        {
                            currentParent.Children.Add(node);
                        }
                    }

                    currentParent = nodeDict[currentPath];
                }
            }

            return rootNodes;
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

            // 設定左邊資訊
            if (item.Left != null)
            {
                node.LeftName = item.Left.Name;
                if (!item.Left.IsDirectory)
                {
                    node.LeftSize = FileSystemNode.FormatSize(item.Left.Size);
                }
                node.LeftDate = item.Left.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }

            // 設定右邊資訊
            if (item.Right != null)
            {
                node.RightName = item.Right.Name;
                if (!item.Right.IsDirectory)
                {
                    node.RightSize = FileSystemNode.FormatSize(item.Right.Size);
                }
                node.RightDate = item.Right.ModifiedTime.ToString("yyyy/MM/dd HH:mm");
            }

            // 設定狀態符號
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

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var node in _treeNodes)
            {
                node.ExpandAll();
            }
            ExpandTreeViewItems(ResultTreeView);
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var node in _treeNodes)
            {
                node.CollapseAll();
            }
            CollapseTreeViewItems(ResultTreeView);
        }

        private void ExpandTreeViewItems(ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                var treeViewItem = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem != null)
                {
                    treeViewItem.IsExpanded = true;
                    ExpandTreeViewItems(treeViewItem);
                }
            }
        }

        private void CollapseTreeViewItems(ItemsControl itemsControl)
        {
            foreach (var item in itemsControl.Items)
            {
                var treeViewItem = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem != null)
                {
                    treeViewItem.IsExpanded = false;
                    CollapseTreeViewItems(treeViewItem);
                }
            }
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
            var selectedItems = GetSelectedTreeNodes();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("請選擇至少一個項目進行同步");
                return;
            }

            string targetRoot = toLeft ? LeftPathBox.Text.Trim() : RightPathBox.Text.Trim();
            string sourceRoot = toLeft ? RightPathBox.Text.Trim() : LeftPathBox.Text.Trim();

            int successCount = 0;
            int errorCount = 0;

            foreach (var node in selectedItems)
            {
                if (node.IsDirectory) continue; // 跳過目錄

                try
                {
                    string sourcePath = Path.Combine(sourceRoot, node.RelativePath);
                    string targetPath = Path.Combine(targetRoot, node.RelativePath);

                    // 確保目標目錄存在
                    var targetDir = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    // 只複製存在於來源端的檔案
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

            // 重新比較更新結果
            Compare_Click(null, null);
        }

        private List<TreeNode> GetSelectedTreeNodes()
        {
            var selected = new List<TreeNode>();
            // 這裡需要實現選擇邏輯，因為TreeView的選擇機制比較複雜
            // 為了簡化，暫時返回所有節點中狀態不為Identical的節點
            CollectNonIdenticalNodes(_treeNodes, selected);
            return selected;
        }

        private void CollectNonIdenticalNodes(IEnumerable<TreeNode> nodes, List<TreeNode> result)
        {
            foreach (var node in nodes)
            {
                if (node.Status != ComparisonStatus.Identical)
                {
                    result.Add(node);
                }
                CollectNonIdenticalNodes(node.Children, result);
            }
        }

        // 將以下方法添加到 MainWindow.xaml.cs 類中

        private void ContextMenu_Properties_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultTreeView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }

            ShowPropertiesDialog(selectedItem);
        }

        private void ContextMenu_CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultTreeView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }

            CopySelectedItemTo(selectedItem, toLeft: true);
        }

        private void ContextMenu_CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultTreeView.SelectedItem as TreeNode;
            if (selectedItem == null)
            {
                MessageBox.Show("請選擇一個項目");
                return;
            }

            CopySelectedItemTo(selectedItem, toLeft: false);
        }

        private void ContextMenu_OpenLeftLocation_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultTreeView.SelectedItem as TreeNode;
            if (selectedItem == null || selectedItem.LeftNode == null)
            {
                MessageBox.Show("左側路徑不存在");
                return;
            }

            OpenFileLocation(selectedItem.LeftNode.FullPath);
        }

        private void ContextMenu_OpenRightLocation_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ResultTreeView.SelectedItem as TreeNode;
            if (selectedItem == null || selectedItem.RightNode == null)
            {
                MessageBox.Show("右側路徑不存在");
                return;
            }

            OpenFileLocation(selectedItem.RightNode.FullPath);
        }

        // 輔助方法
        private void ShowPropertiesDialog(TreeNode node)
        {
            var properties = new StringBuilder();
            properties.AppendLine($"名稱: {node.Name}");
            properties.AppendLine($"狀態: {GetStatusText(node.Status)}");
            properties.AppendLine($"相對路徑: {node.RelativePath}");
            properties.AppendLine($"類型: {(node.IsDirectory ? "資料夾" : "檔案")}");

            if (node.LeftNode != null)
            {
                properties.AppendLine();
                properties.AppendLine("左側:");
                properties.AppendLine($"  完整路徑: {node.LeftNode.FullPath}");
                if (!node.LeftNode.IsDirectory)
                {
                    properties.AppendLine($"  大小: {node.LeftSize}");
                }
                properties.AppendLine($"  修改時間: {node.LeftDate}");
            }

            if (node.RightNode != null)
            {
                properties.AppendLine();
                properties.AppendLine("右側:");
                properties.AppendLine($"  完整路徑: {node.RightNode.FullPath}");
                if (!node.RightNode.IsDirectory)
                {
                    properties.AppendLine($"  大小: {node.RightSize}");
                }
                properties.AppendLine($"  修改時間: {node.RightDate}");
            }

            MessageBox.Show(properties.ToString(), "檔案屬性", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopySelectedItemTo(TreeNode node, bool toLeft)
        {
            if (node.IsDirectory)
            {
                MessageBox.Show("目前不支援複製整個資料夾，請選擇個別檔案");
                return;
            }

            string sourceRoot = toLeft ? RightPathBox.Text.Trim() : LeftPathBox.Text.Trim();
            string targetRoot = toLeft ? LeftPathBox.Text.Trim() : RightPathBox.Text.Trim();

            // 檢查來源是否存在
            var sourceNode = toLeft ? node.RightNode : node.LeftNode;
            if (sourceNode == null)
            {
                MessageBox.Show($"來源檔案不存在於{(toLeft ? "右側" : "左側")}");
                return;
            }

            try
            {
                string sourcePath = sourceNode.FullPath;
                string targetPath = Path.Combine(targetRoot, node.RelativePath);

                // 確保目標目錄存在
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                // 複製檔案
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, targetPath, true);
                    MessageBox.Show($"檔案已成功複製到{(toLeft ? "左側" : "右側")}");

                    // 重新比較更新結果
                    Compare_Click(null, null);
                }
                else
                {
                    MessageBox.Show("來源檔案不存在");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"複製檔案失敗: {ex.Message}");
            }
        }

        private void OpenFileLocation(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // 在檔案總管中選中該檔案
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(filePath))
                {
                    // 開啟資料夾
                    System.Diagnostics.Process.Start("explorer.exe", filePath);
                }
                else
                {
                    MessageBox.Show("路徑不存在");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法開啟檔案位置: {ex.Message}");
            }
        }

        private string GetStatusText(ComparisonStatus status)
        {
            return status switch
            {
                ComparisonStatus.Identical => "相同",
                ComparisonStatus.Modified => "已修改",
                ComparisonStatus.LeftOnly => "僅存於左側",
                ComparisonStatus.RightOnly => "僅存於右側",
                ComparisonStatus.Conflict => "衝突",
                _ => "未知"
            };
        }
    }
}
