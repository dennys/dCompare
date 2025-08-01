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
            properties.AppendLine($"{Properties.Resources.PropertyName}: {node.Name}");
            properties.AppendLine($"{Properties.Resources.PropertyStatus}: {GetStatusText(node.Status)}");
            properties.AppendLine($"{Properties.Resources.PropertyRelativePath}: {node.RelativePath}");
            properties.AppendLine($"{Properties.Resources.PropertyType}: {(node.IsDirectory ? Properties.Resources.PropertyFolder : Properties.Resources.PropertyFile)}");

            if (node.LeftNode != null)
            {
                properties.AppendLine();
                properties.AppendLine(Properties.Resources.PropertyLeft + ":");
                properties.AppendLine($"  {Properties.Resources.PropertyFullPath}: {node.LeftNode.FullPath}");
                if (!node.LeftNode.IsDirectory)
                {
                    properties.AppendLine($"  {Properties.Resources.PropertySize}: {node.LeftSize}");
                }
                properties.AppendLine($"  {Properties.Resources.PropertyModifiedTime}: {node.LeftDate}");
            }

            if (node.RightNode != null)
            {
                properties.AppendLine();
                properties.AppendLine(Properties.Resources.PropertyRight + ":");
                properties.AppendLine($"  {Properties.Resources.PropertyFullPath}: {node.RightNode.FullPath}");
                if (!node.RightNode.IsDirectory)
                {
                    properties.AppendLine($"  {Properties.Resources.PropertySize}: {node.RightSize}");
                }
                properties.AppendLine($"  {Properties.Resources.PropertyModifiedTime}: {node.RightDate}");
            }

            MessageBox.Show(properties.ToString(), Properties.Resources.FileProperties, MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show(Properties.Resources.PathNotExist);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.CannotOpenFileLocation, ex.Message));
            }
        }

        private static string GetStatusText(ComparisonStatus status)
        {
            return status switch
            {
                ComparisonStatus.Identical => Properties.Resources.StatusIdentical,
                ComparisonStatus.Modified => Properties.Resources.StatusModified,
                ComparisonStatus.LeftOnly => Properties.Resources.StatusLeftOnly,
                ComparisonStatus.RightOnly => Properties.Resources.StatusRightOnly,
                ComparisonStatus.Conflict => Properties.Resources.StatusConflict,
                _ => Properties.Resources.StatusUnknown
            };
        }
    }
}