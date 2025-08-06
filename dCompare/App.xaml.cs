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
        public static IHost AppHost { get; private set; }

        public App()
        {
            // Configuration Setup
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Constant.DefaultConfigurationFile, optional: true, reloadOnChange: true)
                .Build();

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

            // Configuration for the logger
            IConfigurationRoot loggerConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Serilog.json", optional: true, reloadOnChange: true)
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
                    services.AddSingleton<IFileOperationStrategy, Kernel32FileCopyStrategy>();
                    services.AddSingleton<IFileSystem, FileSystem>();
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
            await AppHost.StopAsync();
            AppHost.Dispose();
            Log.Information("Application shutting down.");
            Log.CloseAndFlush(); // Ensures all logs are written before the app closes
            base.OnExit(e);
        }
    }
}