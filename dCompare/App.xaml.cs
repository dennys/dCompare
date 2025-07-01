using dCompare.Compare;
using dCompare.Configuration;
using dCompare.FileOperation;
using dCompare.FileOperation.Strategies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Windows;

namespace dCompare
{
    public partial class App : Application
    {
        public static IHost AppHost { get; private set; }

        public App()
        {
            // --- Serilog Static Logger Setup ---
            // Build a config object to read the json file
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("dcompare.json", optional: false, reloadOnChange: true)
                .Build();

            // Configure the static logger
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
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
                    services.AddTransient<UcCompare>(); // Crucial: Transient so each tab gets a new instance

                    // Register Services as Singletons
                    services.AddSingleton<IConfigurationService, ConfigurationService>();
                    services.AddSingleton<ISessionManager, SessionManager>();
                    services.AddSingleton<IFileManager, FileManager>();

                    // Register other dependencies
                    services.AddSingleton<IFileOperationStrategy, Kernel32FileCopyStrategy>();
                    services.AddSingleton<IFileSystem, FileSystem>();
                })
                // --- Integrate Serilog with the Host ---
                .UseSerilog() // This replaces the default logger with Serilog
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                Log.Information("Application starting up.");
                await AppHost!.StartAsync();
                var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
                startupForm.Show();
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application failed to start.");
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await AppHost.StopAsync();
            Log.Information("Application shutting down.");
            Log.CloseAndFlush(); // IMPORTANT: Ensures all logs are written before the app closes
            base.OnExit(e);
        }
    }
}