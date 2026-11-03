using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare;
using dCompare.Compare;
using dCompare.Configuration;
using dCompare.FileOperation;
using dCompare.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;

namespace dCompare.Tests
{
    /// <summary>
    /// Process-wide service provider for tests that need the CommunityToolkit <see cref="Ioc.Default"/>
    /// (e.g. CompareControl, whose ToolHandler resolves from it). Configure exactly once: the first
    /// caller wins, all callers get the same provider.
    /// </summary>
    public static class TestIoc
    {
        private static readonly object _lock = new();
        private static IServiceProvider? _provider;

        public static IServiceProvider EnsureConfigured()
        {
            lock (_lock)
            {
                if (_provider == null)
                {
                    var services = new ServiceCollection();
                    string sharedConfigDir = Path.Combine(Path.GetTempPath(), "dCompareTests", "shared");
                    Directory.CreateDirectory(sharedConfigDir);
                    services.AddSingleton<IConfigurationService>(new ConfigurationService(
                        Path.Combine(sharedConfigDir, "dCompare.json")));
                    services.AddSingleton<IFileManager>(new FileManager());
                    services.AddSingleton<IUserInteractionService>(new SilentUserInteractionService());
                    services.AddTransient<CompareControl>(); // same as App: transient so each tab gets a new instance
                    services.AddSingleton<IFileSystem>(new FileSystem());
                    services.AddSingleton<IHashProvider>(new HashProvider());
                    services.AddSingleton<ILogger<ToolFactory>>(NullLogger<ToolFactory>.Instance);
                    services.AddSingleton<ILogger<CompareControl>>(NullLogger<CompareControl>.Instance);
                    services.AddSingleton<ILogger<MainWindowViewModel>>(NullLogger<MainWindowViewModel>.Instance);
                    _provider = services.BuildServiceProvider();
                    Ioc.Default.ConfigureServices(_provider);
                }
                return _provider;
            }
        }

        // A test stand-in that never opens a dialog: a stray ShowError/ShowConfirmation on the
        // test thread would hang the suite on a real MessageBox.
        private class SilentUserInteractionService : IUserInteractionService
        {
            public void ShowError(string message, string title) { }
            public void ShowInformation(string message, string title) { }
            public bool ShowConfirmation(string message, string title) => false;
            public string PromptForTabName(string currentName, string prompt, string title) => currentName;
        }
    }
}
