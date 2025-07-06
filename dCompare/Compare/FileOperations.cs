using dCompare.Compare.Models;
using Serilog;
using System.IO;
using System.Text;
using System.Windows;

namespace dCompare.Compare
{
    public static class FileOperations
    {
        public static void ShowPropertiesDialog(TreeNode node)
        {
            StringBuilder properties = new();
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