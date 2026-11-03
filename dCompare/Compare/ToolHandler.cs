using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare.Common;
using dCompare.Compare.Models;
using dCompare.Configuration;
using dCompare.Tools;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Compare
{
    public class ToolHandler
    {
        private readonly IConfigurationService _configService;
        private readonly ToolConfigService _toolConfigService;
        private readonly ToolFactory _toolFactory;

        public ToolHandler()
        {
            _configService = Ioc.Default.GetRequiredService<IConfigurationService>();
            var logger = Ioc.Default.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ToolFactory>>();
            _toolFactory = new(_configService, logger);
            _toolConfigService = new();
        }

        public async Task<TabItem?> LaunchToolAsync(TreeNode selectedItem, ToolOperationTypes operation, ToolOptions? toolConfig = null)
        {
            if (selectedItem.IsDirectory)
            {
                MessageBox.Show(
                    operation == ToolOperationTypes.View ? Properties.Resources.CannotOpenDirectory : Properties.Resources.CannotCompareDirectory,
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            // An archive entry's path is archive file + entry key: no reader can open it, so viewing or diffing
            // one only ever ends in a load error a few frames later. Say so here, where it is understood.
            if (selectedItem.IsArchiveEntry)
            {
                MessageBox.Show(
                    Properties.Resources.ArchiveEntryOperationNotSupported,
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            try
            {
                // Determine the file path(s) based on operation
                string? path = selectedItem.LeftNode?.FullPath ?? selectedItem.RightNode?.FullPath;
                string? leftPath = operation == ToolOperationTypes.View ? path : (selectedItem.LeftNode?.FullPath ?? EmptyStandInFile.Value);
                string? rightPath = operation == ToolOperationTypes.View ? null : (selectedItem.RightNode?.FullPath ?? EmptyStandInFile.Value);

                if (toolConfig == null)
                {
                    string pathForExtension = path ?? leftPath; // Use available path for extension
                    var config = await _configService.LoadConfigurationAsync();
                    string extension = Path.GetExtension(pathForExtension);
                    toolConfig = _toolConfigService.GetDefaultTool(config, operation, extension);

                    if (toolConfig == null)
                    {
                        MessageBox.Show(
                            string.Format(Properties.Resources.NoDefaultTool, operation == ToolOperationTypes.View ? Properties.Resources.View : Properties.Resources.Diff, extension),
                            Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }
                }

                if (toolConfig.Type == ToolTypes.BuiltIn)
                {
                    ITool? tool = operation == ToolOperationTypes.View   // Create tool based on operation
                        ? await _toolFactory.CreateToolAsync(leftPath)
                        : await _toolFactory.CreateToolAsync(leftPath, rightPath ?? string.Empty); // Ensure rightPath is not null

                    if (tool == null)
                    {
                        return null; // Tool creation failed, error already shown.
                    }

                    if (operation == ToolOperationTypes.View)
                    {
                        tool.LoadFile(leftPath);
                    }

                    string tabHeader = operation == ToolOperationTypes.View
                        ? string.Format(Properties.Resources.ViewTabHeader, Path.GetFileName(leftPath))
                        : string.Format(Properties.Resources.CompareTabHeader, Path.GetFileName(leftPath), Path.GetFileName(rightPath));

                    var newTab = new TabItem { Header = tabHeader, Content = tool.GetControl() };
                    return newTab;
                }
                else // External
                {
                    LaunchExternalTool(toolConfig, operation, leftPath, rightPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Properties.Resources.ErrorTool, operation == ToolOperationTypes.View ? Properties.Resources.ErrorOpeningFile : Properties.Resources.ErrorLaunchingTool, ex.Message),
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        /// <summary>
        /// Launch an external tool with UseShellExecute=false so cmd.exe never re-reads the command line
        /// (&amp;, |, ^ in a file name would become operators) and every substituted path is quoted.
        /// </summary>
        private void LaunchExternalTool(ToolOptions toolConfig, ToolOperationTypes operation, string? leftPath, string? rightPath)
        {
            string? toolPath = ResolveToolPath(toolConfig.Path);
            if (toolPath == null)
            {
                Log.Error("External tool '{ToolPath}' ({Operation}) was not found.", toolConfig.Path, operation);
                MessageBox.Show(
                    string.Format(Properties.Resources.ErrorTool, Properties.Resources.ErrorLaunchingTool, Properties.Resources.ErrorFileOrDirectoryNotFound),
                    Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string arguments = operation == ToolOperationTypes.View
                ? SubstituteQuotedPath(toolConfig.ViewArguments, "{file}", leftPath)
                : SubstituteQuotedPath(SubstituteQuotedPath(toolConfig.DiffArguments, "{left}", leftPath), "{right}", rightPath);

            string fileName = toolPath;
            string extension = Path.GetExtension(toolPath);
            if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                // Batch scripts cannot be started directly without the shell; residual risk: cmd.exe still
                // parses the argument string, so quoting is what keeps a crafted file name in one piece.
                // cmd /c's documented quote handling strips one outer pair of quotes from the rest of the
                // line, so the whole command carries its own pair: /c ""C:\t.bat" "arg"" -> "C:\t.bat" "arg".
                // Without that, a script path containing spaces loses its quotes and fails to resolve.
                fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                string command = $"\"{toolPath}\" {arguments}".TrimEnd();
                arguments = $"/c \"{command}\"";
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false
            });
        }

        /// <summary>Replace {placeholder} with the quoted path; a template that already wraps the
        /// placeholder in quotes ("{file}") is matched with those quotes so nothing ends up double quoted.</summary>
        private static string SubstituteQuotedPath(string template, string placeholder, string? path)
        {
            if (string.IsNullOrEmpty(template) || !template.Contains(placeholder, StringComparison.Ordinal)) return template;
            string quotedPath = $"\"{path ?? string.Empty}\"";
            string quotedPlaceholder = $"\"{placeholder}\"";
            return template.Contains(quotedPlaceholder, StringComparison.Ordinal)
                ? template.Replace(quotedPlaceholder, quotedPath)
                : template.Replace(placeholder, quotedPath);
        }

        /// <summary>Find the configured tool. A bare name is looked up where UseShellExecute=true used to
        /// find it (app folder, system folder, PATH, Program Files) and gets ".exe" appended when it has no
        /// extension, because ProcessStartInfo no longer goes through the shell.</summary>
        private static string? ResolveToolPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return null;
            // The shell used to expand these for us.
            configuredPath = Environment.ExpandEnvironmentVariables(configuredPath);
            if (Path.IsPathRooted(configuredPath)) return Probe(configuredPath);

            foreach (string directory in ToolSearchDirectories())
            {
                string? candidate = Probe(Path.Combine(directory, configuredPath));
                if (candidate != null) return candidate;
            }
            return null;
        }

        private static string? Probe(string candidate)
        {
            if (File.Exists(candidate)) return candidate;
            string withExtension = candidate + ".exe";
            return File.Exists(withExtension) ? withExtension : null;
        }

        private static IEnumerable<string> ToolSearchDirectories() =>
            new[]
            {
                AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(directory => !string.IsNullOrWhiteSpace(directory) && Path.IsPathRooted(directory));
        // One empty file per process stands in for the missing side of a one-sided diff. Nothing writes to it, so
        // it is created once instead of a new GUID-named file per diff (those were never cleaned up). Named per
        // process rather than fixed, so two tabs or two copies of the app diffing at once cannot lock each other
        // out of the same file.
        private static readonly Lazy<string> EmptyStandInFile = new(() =>
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"dCompare_Empty_{Environment.ProcessId}.txt");
            if (!File.Exists(tempPath))
            {
                File.WriteAllText(tempPath, string.Empty);
            }
            return tempPath;
        });
    }
}
