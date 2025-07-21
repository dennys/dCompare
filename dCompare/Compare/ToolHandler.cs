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
    public class ToolHandler
	{
		ToolFactory? _toolFactory;

		public async Task<TabItem?> LaunchToolAsync(TreeNode selectedItem, string operation, ToolOptions? toolConfig = null)
		{
			var _configService = Ioc.Default.GetRequiredService<IConfigurationService>();
			_toolFactory = new ToolFactory(_configService);

			ToolConfigService _toolConfigService = new();

			if (selectedItem.IsDirectory)
			{
				MessageBox.Show(
					operation == Operations.View ? "Cannot open a directory." : "Cannot compare directories with a file comparison tool.",
					Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}
			if (String.IsNullOrEmpty(operation))
			{
				MessageBox.Show("Invalid operation specified.", Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
							$"No default {(operation == Operations.View ? "view" : "diff")} tool configured for '{extension}' files.",
							Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
						return null;
					}
				}

				if (toolConfig.Type == "BuiltIn")
				{
                    ITool tool = operation == Operations.View   // Create tool based on operation
                        ? _toolFactory.CreateTool(leftPath)
                        : _toolFactory.CreateTool(leftPath, rightPath ?? string.Empty); // Ensure rightPath is not null

                    if (operation == Operations.View)
                    {
                        tool.LoadFile(leftPath);
                    }
                    
					string tabHeader = operation == Operations.View
						? $"View: {Path.GetFileName(leftPath)}"
						: $"Compare: {Path.GetFileName(leftPath)} - {Path.GetFileName(rightPath)}";

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
					$"Error {(operation == Operations.View ? "opening file" : "launching comparison tool")}: {ex.Message}",
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
