using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare.Common;
using dCompare.Compare;
using dCompare.Configuration;
using dCompare.FileOperation;
using dCompare.FileOperation.Strategies;
using dCompare.Session;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Globalization;
using System.IO;
using System.Windows;

namespace dCompare
{
    public partial class App : Application
    {
        public static IHost AppHost { get; private set; } = null!;

        public App()
        {
            // Configuration for the logger
            IConfigurationRoot loggerConfiguration = new ConfigurationBuilder()
                .SetBasePath(AppPaths.BaseDirectory)
                .AddJsonFile("Serilog.json", optional: true, reloadOnChange: true)
                // Added last so it wins over Serilog.json: the file sink must write under the exe folder,
                // not relative to whatever the working directory happens to be.
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Serilog:WriteTo:0:Args:path"] = AppPaths.LogFile })
                .Build();

            // Configure the static logger
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(loggerConfiguration)
                .Enrich.WithThreadId()
                .CreateLogger();

            // Older builds persisted next to the working directory; move those files to the portable home.
            AppPaths.MigrateFromWorkingDirectory(Constant.DefaultConfigurationFile, Constant.RecentSessionsFile);

            // Configuration Setup
            // A corrupt dCompare.json must not prevent the app from starting (the Options page is the
            // repair path); fall back to an empty configuration and let the first
            // LoadConfigurationAsync surface the error to the user.
            IConfigurationRoot configuration;
            try
            {
                // Same file ConfigurationService reads: the exe folder first, then the %LOCALAPPDATA% copy it
                // writes when the exe folder is read-only. Resolving this from the exe folder alone meant the
                // language saved by the Options page was never the language applied at startup.
                configuration = new ConfigurationBuilder()
                    .AddJsonFile(AppPaths.ResolveExisting(Constant.DefaultConfigurationFile), optional: true, reloadOnChange: true)
                    .Build();
            }
            catch (InvalidDataException ex)
            {
                Log.Error(ex, "Configuration file is corrupt; starting with an empty configuration.");
                configuration = new ConfigurationBuilder().Build();
            }

            // Configure Culture
            string languageCode = configuration["Options:UI:Language"] ?? Constant.DefaultLanguageCode;
            if (!string.IsNullOrEmpty(languageCode))
            {
                try
                {
                    CultureInfo culture = new(languageCode);
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                }
                catch (CultureNotFoundException ex)
                {
                    Log.Warning(ex, "Invalid culture code '{LanguageCode}', falling back to 'en'", languageCode);
                    CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(Constant.DefaultLanguageCode);
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(Constant.DefaultLanguageCode);
                }
            }

            // --- Host Setup ---
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    // Register Window and UserControls
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddTransient<CompareControl>(); // Crucial: Transient so each tab gets a new instance

                    // Register Services as Singletons
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<ISessionManager, SessionManager>();
                    services.AddSingleton<IFileManager, FileManager>();
                    services.AddSingleton<IFileOperationStrategy, Kernel32FileCopyStrategy>();
                    services.AddSingleton<IFileSystem, FileSystem>();
                    services.AddSingleton<IHashProvider, HashProvider>();
                    services.AddSingleton<IUserInteractionService, UserInteractionService>();
                })
            // --- Integrate Serilog with the Host ---
            .UseSerilog() // This replaces the default logger with Serilog
            .Build();

            // Configure CommunityToolkit.Mvvm IoC with the Host's service provider
            Ioc.Default.ConfigureServices(AppHost.Services);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                Log.Information("Application starting up.");
                await AppHost.StartAsync();
                MainWindow mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application failed to start.");
                MessageBox.Show($"Application failed to start: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown(1);
                return;
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // async void at the framework boundary: swallow and log, never let an exception escape
            // (an unhandled exit-time exception can crash the process after the window closed).
            try
            {
                await AppHost.StopAsync();
                AppHost.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while stopping the host on exit.");
            }
            finally
            {
                try
                {
                    Log.Information("Application shutting down.");
                    Log.CloseAndFlush(); // Ensures all logs are written before the app closes
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Error while closing the log on exit.");
                }
            }
            base.OnExit(e);
        }
    }
}