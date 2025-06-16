using dCompare.Configuration;
using System.ComponentModel;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace dCompare.Options
{
    public partial class UIGeneralConfigView : UserControl, INotifyPropertyChanged
    {
        private readonly UIConfig _uiConfig;
        private readonly ConfigurationService _configurationService;

        public UIGeneralConfigView(ConfigurationService configurationService)
        {
            InitializeComponent();
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _uiConfig = _configurationService.GetConfiguration().Options.UI;
            DataContext = this;
        }

        public string Theme
        {
            get => _uiConfig.Theme;
            set
            {
                if (_uiConfig.Theme != value)
                {
                    _uiConfig.Theme = value;
                    OnPropertyChanged(nameof(Theme));
                    SaveConfiguration();
                }
            }
        }

        public string Language
        {
            get => _uiConfig.Language;
            set
            {
                if (_uiConfig.Language != value)
                {
                    _uiConfig.Language = value;
                    OnPropertyChanged(nameof(Language));
                    SaveConfiguration();
                }
            }
        }

        private void SaveConfiguration()
        {
            var config = _configurationService.GetConfiguration();
            config.Options.UI = _uiConfig;
            _configurationService.SaveConfiguration(config);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}