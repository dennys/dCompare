using dCompare.Configuration;
using System.ComponentModel;
using System.Windows.Controls;

namespace dCompare.Options
{
    public partial class CompareConfigView : UserControl, INotifyPropertyChanged
    {
        private readonly CompareConfig _compareConfig;

        public CompareConfigView()
        {
            InitializeComponent();
            _compareConfig = (DataContext as CompareConfig) ?? new CompareConfig();
            DataContext = this;
        }

        public string Algorithm
        {
            get => _compareConfig.Algorithm;
            set
            {
                _compareConfig.Algorithm = value;
                OnPropertyChanged(nameof(Algorithm));
            }
        }

        public string DoubleClick
        {
            get => _compareConfig.DoubleClick;
            set
            {
                _compareConfig.DoubleClick = value;
                OnPropertyChanged(nameof(DoubleClick));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}