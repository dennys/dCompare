using CommunityToolkit.Mvvm.DependencyInjection;
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
using System.Threading;
using System.Windows;

namespace dCompare
{
    public partial class App : Application
    {
        public static IHost AppHost { get; private set; }

        public App()
        {
            // --- Configuration Setup ---
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("dCompare.json", optional: true, reloadOnChange: true)
                .Build();

            // Configuration for the logger
            var loggerConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Serilog.json", optional: false, reloadOnChange: true)
                .Build();

            // Configure the static logger
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(loggerConfiguration)
                .Enrich.WithThreadId()
                .CreateLogger();

            // --- Host Setup ---
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, configBuilder) =>
                {
                    // Add the same configuration to the host
                    configBuilder.AddConfiguration(configuration);
                })
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

                    // Register other dependencies
                    services.AddSingleton<IFileOperationStrategy, Kernel32FileCopyStrategy>();
                    services.AddSingleton<IFileSystem, FileSystem>();

                    // Register UserInteractionService for ViewModel
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
                IConfigurationService configService = AppHost.Services.GetRequiredService<IConfigurationService>();
                IConfiguration? configuration = configService.GetConfiguration();
                if (configuration != null)
                {
                    var appConfig = configuration.Get<AppConfiguration>();
                    if (appConfig?.Options?.UI?.Language != null)
                    {
                        Thread.CurrentThread.CurrentUICulture = new CultureInfo(appConfig.Options.UI.Language);
                    }
                }

                Log.Information("Application starting up.");
                await AppHost!.StartAsync();
                var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
                startupForm.Show();
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application failed to start.");
                MessageBox.Show($"Application failed to start: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost.StopAsync();
            AppHost.Dispose();
            Log.Information("Application shutting down.");
            Log.CloseAndFlush(); // Ensures all logs are written before the app closes
            base.OnExit(e);
        }
    }
}