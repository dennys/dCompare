using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.Tools;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Compare
{
    public enum ToolTypes { BuiltIn, External }
    public class ToolHandler
    {
        private readonly IConfigurationService _configService;
        private readonly ToolConfigService _toolConfigService;
        private readonly ToolFactory _toolFactory;

        public ToolHandler()
        {
            _configService = Ioc.Default.GetRequiredService<IConfigurationService>();
            _toolFactory = new(_configService);
            _toolConfigService = new();
        }
        public async Task<TabItem?> LaunchToolAsync(TreeNode selectedItem, string operation, ToolOptions? toolConfig = null)
        {
            if (selectedItem.IsDirectory)
            {
                MessageBox.Show(
                    operation == Operations.View ? Properties.Resources.CannotOpenDirectory : Properties.Resources.CannotCompareDirectory,
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            if (String.IsNullOrEmpty(operation))
            {
                MessageBox.Show(Properties.Resources.InvalidOperation, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            try
            {
                // Determine the file path(s) based on operation
                string? path = selectedItem.LeftNode?.FullPath ?? selectedItem.RightNode?.FullPath;
                string? leftPath = operation == Operations.View ? path : (selectedItem.LeftNode?.FullPath ?? CreateTemporaryEmptyFile());
                //string? leftPath = operation == Operations.View ? path : selectedItem.LeftNode?.FullPath;
                string? rightPath = operation == Operations.View ? null : (selectedItem.RightNode?.FullPath ?? CreateTemporaryEmptyFile());

                //if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(leftPath) || string.IsNullOrEmpty(rightPath))
                //{
                //	MessageBox.Show("No valid file path to open.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                //	return null;
                //}

                if (toolConfig == null)
                {
                    string pathForExtension = path ?? leftPath; // Use available path for extension
                    var config = await _configService.LoadConfigurationAsync();
                    string extension = Path.GetExtension(pathForExtension);
                    toolConfig = _toolConfigService.GetDefaultTool(config, operation, extension);

                    if (toolConfig == null)
                    {
                        MessageBox.Show(
                            string.Format(Properties.Resources.NoDefaultTool, operation == Operations.View ? Properties.Resources.View : Properties.Resources.Diff, extension),
                            Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }
                }

                if (toolConfig.Type == ToolTypes.BuiltIn.ToString())
                {
                    ITool tool = operation == Operations.View   // Create tool based on operation
                        ? _toolFactory.CreateTool(leftPath)
                        : _toolFactory.CreateTool(leftPath, rightPath ?? string.Empty); // Ensure rightPath is not null

                    if (operation == Operations.View)
                    {
                        tool.LoadFile(leftPath);
                    }

                    string tabHeader = operation == Operations.View
                        ? string.Format(Properties.Resources.ViewTabHeader, Path.GetFileName(leftPath))
                        : string.Format(Properties.Resources.CompareTabHeader, Path.GetFileName(leftPath), Path.GetFileName(rightPath));

                    var newTab = new TabItem { Header = tabHeader, Content = tool.GetControl() };
                    return newTab;
                }
                else // External
                {
                    string arguments = operation == Operations.View
                        ? toolConfig.ViewArguments.Replace("{file}", leftPath)
                        : toolConfig.DiffArguments.Replace("{left}", leftPath).Replace("{right}", rightPath);

                    ProcessStartInfo startInfo = new()
                    {
                        FileName = toolConfig.Path,
                        Arguments = arguments,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.ErrorTool, operation == Operations.View ? Properties.Resources.ErrorOpeningFile : Properties.Resources.ErrorLaunchingTool, ex.Message),
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }
        private static string CreateTemporaryEmptyFile()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"dCompare_Empty_{Guid.NewGuid()}.txt");
            File.WriteAllText(tempPath, string.Empty);
            return tempPath;
        }
    }
}
