using dCompare.Configuration;
using System.ComponentModel;
using System.Windows.Controls;

namespace dCompare.Options
{
    public partial class DiffConfigView : UserControl, INotifyPropertyChanged
    {
        private readonly DiffConfig _diffConfig;

        public DiffConfigView()
        {
            InitializeComponent();
            _diffConfig = (DataContext as DiffConfig) ?? new DiffConfig();
            DataContext = this;
        }

        public TextDiffConfig Text
        {
            get => _diffConfig.Text;
            set
            {
                _diffConfig.Text = value;
                OnPropertyChanged(nameof(Text));
            }
        }

        public ImageDiffConfig Image
        {
            get => _diffConfig.Image;
            set
            {
                _diffConfig.Image = value;
                OnPropertyChanged(nameof(Image));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}