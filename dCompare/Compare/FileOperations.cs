using dCompare.Compare;
using System;
using System.IO;
using System.Text;
using System.Windows;

namespace dCompare
{
    public static class FileOperations
    {
        public static void CopySelectedItemTo(TreeNode node, bool toLeft, string leftPath, string rightPath)
        {
            if (node.IsDirectory) { MessageBox.Show("目前不支援複製整個資料夾，請選擇個別檔案"); return; }
            string sourceRoot = toLeft ? rightPath : leftPath;
            string targetRoot = toLeft ? leftPath : rightPath;

            FileSystemNode? sourceNode = toLeft ? node.RightNode : node.LeftNode;
            if (sourceNode == null)
            {
                MessageBox.Show($"來源檔案不存在於{(toLeft ? "右側" : "左側")}");
                return;
            }

            try
            {
                string sourcePath = sourceNode.FullPath;
                string targetPath = Path.Combine(targetRoot, node.RelativePath);

                string? targetDir = Path.GetDirectoryName(targetPath);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, targetPath, true);
                    MessageBox.Show($"檔案已成功複製到{(toLeft ? "左側" : "右側")}");
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

        public static void ShowPropertiesDialog(TreeNode node)
        {
            StringBuilder properties = new StringBuilder();
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

        public static void OpenFileLocation(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                else if (Directory.Exists(filePath))
                {
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

        private static string GetStatusText(ComparisonStatus status)
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