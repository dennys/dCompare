using dCompare.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace dCompare
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; }

        public App()
        {
            InitializeComponent();
            ConfigureServices();
        }

        private void ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ConfigurationService>();
            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            StartupAsync().GetAwaiter().GetResult();
        }

        private async Task StartupAsync()
        {
            // Set default culture first to ensure the app starts in a known state
            var defaultCulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = defaultCulture;
            Thread.CurrentThread.CurrentCulture = defaultCulture;

            // Variable to store any exception
            Exception configException = null;

            try
            {
                var configService = ServiceProvider.GetRequiredService<ConfigurationService>();
                var config = await configService.LoadConfigurationAsync();
                var language = config?.Options?.UI?.Language ?? "en";
                var culture = new CultureInfo(language);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
            }
            catch (Exception ex)
            {
                // Store the exception
                configException = ex;
                System.Diagnostics.Debug.WriteLine($"Configuration error: {ex.Message}");
            }

            // Always create and show the MainWindow
            var mainWindow = new MainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Show();

            // Show error message if an exception occurred
            if (configException != null)
            {
                MessageBox.Show(mainWindow,
                    $"Failed to load configuration: {configException.Message}\nUsing default language (English).",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Empty or removed if not needed
        }

        private void ChangeLanguage()
        {
            var newCulture = new CultureInfo("en");
            Thread.CurrentThread.CurrentUICulture = newCulture;
            Thread.CurrentThread.CurrentCulture = newCulture;

            var oldWindow = Current.MainWindow;
            var newWindow = new MainWindow();
            Current.MainWindow = newWindow;
            newWindow.Show();
            oldWindow.Close();
        }
    }
}