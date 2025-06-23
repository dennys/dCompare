using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using dCompare.Configuration;

namespace i18n
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ConfigurationService _configService;
        private AppConfiguration _config;
        public MainWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            Loaded += MainWindow_Loaded;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _config = await _configService.LoadConfigurationAsync();
            ApplyTheme(_config.Options.UI.Theme);
            SetLanguage(_config.Options.UI.Language);
            UpdateUIFromConfig();
        }

        private void ApplyTheme(string themeName)
        {
            // Remove existing theme dictionaries
            var existingDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Theme.xaml"));

            if (existingDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingDict);
            }

            // Add the new theme
            var themeUri = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative);
            var themeDict = new ResourceDictionary { Source = themeUri };
            Application.Current.Resources.MergedDictionaries.Add(themeDict);

            // Set window styles dynamically
            this.Background = (System.Windows.Media.Brush)Application.Current.Resources["BackgroundColor"];
            this.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ForegroundColor"];
        }


        private void SetLanguage(string cultureName)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);

            // To update bindings dynamically, you would typically need a more
            // complex setup with a localization provider. For this simple example,
            // we will just reset the content of the labels manually.
            //this.FindResource("ThemeLabel"); // This will trigger a lookup with the new culture
            //this.FindResource("LanguageLabel");
            //this.FindResource("GreetingText");
        }

        private void UpdateUIFromConfig()
        {
            // Set Theme ComboBox
            var themeItem = ThemeComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString().Equals(_config.Options.UI.Theme, StringComparison.OrdinalIgnoreCase));
            if (themeItem != null)
            {
                ThemeComboBox.SelectedItem = themeItem;
            }

            // Set Language ComboBox
            var langItem = LanguageComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString().Equals(_config.Options.UI.Language, StringComparison.OrdinalIgnoreCase));
            if (langItem != null)
            {
                LanguageComboBox.SelectedItem = langItem;
            }
        }

        private async void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem selectedItem)
            {
                var themeName = selectedItem.Content.ToString();
                if (_config != null && _config.Options.UI.Theme != themeName)
                {
                    ApplyTheme(themeName);
                    _config.Options.UI.Theme = themeName;
                    await _configService.SaveConfigurationAsync(_config);
                }
            }
        }

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem selectedItem)
            {
                var cultureName = selectedItem.Tag.ToString();
                if (_config != null && _config.Options.UI.Language != cultureName)
                {
                    _config.Options.UI.Language = cultureName;
                    await _configService.SaveConfigurationAsync(_config);

                    // Show a message box to restart the app for language changes
                    MessageBox.Show("Please restart the application for the language change to take full effect.", "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}