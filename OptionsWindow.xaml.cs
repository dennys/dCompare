using System.Windows;
using dCompare.Configuration;

namespace dCompare
{
    public partial class OptionsWindow : Window
    {
        private readonly ConfigurationService _configService;
        private OptionsConfig _options;

        public OptionsWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _options = _configService.GetConfiguration().Options;
            DataContext = _options; // Bind to UI elements
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            _configService.UpdateOptions(_options);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}