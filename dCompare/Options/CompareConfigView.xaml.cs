using dCompare.Configuration;
using System.ComponentModel;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace dCompare.Options
{
    public partial class CompareConfigView : UserControl, INotifyPropertyChanged
    {
        private readonly CompareConfig _compareConfig;
        private readonly ConfigurationService _configurationService;

        public CompareConfigView(ConfigurationService configurationService)
        {
            InitializeComponent();
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _compareConfig = _configurationService.GetConfiguration().Options.Compare;
            DataContext = this;
        }

        public string Algorithm
        {
            get => _compareConfig.Algorithm;
            set
            {
                if (_compareConfig.Algorithm != value)
                {
                    _compareConfig.Algorithm = value;
                    OnPropertyChanged(nameof(Algorithm));
                    SaveConfiguration();
                }
            }
        }

        public string DoubleClick
        {
            get => _compareConfig.DoubleClick;
            set
            {
                if (_compareConfig.DoubleClick != value)
                {
                    _compareConfig.DoubleClick = value;
                    OnPropertyChanged(nameof(DoubleClick));
                    SaveConfiguration();
                }
            }
        }

        private void SaveConfiguration()
        {
            var config = _configurationService.GetConfiguration();
            config.Options.Compare = _compareConfig;
            _configurationService.SaveConfiguration(config);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}