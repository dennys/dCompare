using dCompare.Configuration;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Options
{
    public partial class DiffConfigView : UserControl, INotifyPropertyChanged
    {
        private readonly DiffConfig _diffConfig;
        private string _selectedTextExt;
        private string _selectedImageExt;

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

        public string SelectedTextExt
        {
            get => _selectedTextExt;
            set
            {
                _selectedTextExt = value;
                OnPropertyChanged(nameof(SelectedTextExt));
            }
        }

        public string SelectedImageExt
        {
            get => _selectedImageExt;
            set
            {
                _selectedImageExt = value;
                OnPropertyChanged(nameof(SelectedImageExt));
            }
        }

        private void AddTextExtension_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateExtension(TextExtCombo.Text, out string normalizedExt))
            {
                if (!Text.Ext.Contains(normalizedExt))
                {
                    Text.Ext.Add(normalizedExt);
                    OnPropertyChanged(nameof(Text));
                    TextExtCombo.Text = string.Empty;
                }
                else
                {
                    MessageBox.Show("Extension already exists.", "Duplicate Extension", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RemoveTextExtension_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SelectedTextExt) && Text.Ext.Contains(SelectedTextExt))
            {
                Text.Ext.Remove(SelectedTextExt);
                SelectedTextExt = null; // Clear selection
                OnPropertyChanged(nameof(Text));
            }
            else
            {
                MessageBox.Show("Select an extension to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddImageExtension_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateExtension(ImageExtCombo.Text, out string normalizedExt))
            {
                if (!Image.Ext.Contains(normalizedExt))
                {
                    Image.Ext.Add(normalizedExt);
                    OnPropertyChanged(nameof(Image));
                    ImageExtCombo.Text = string.Empty;
                }
                else
                {
                    MessageBox.Show("Extension already exists.", "Duplicate Extension", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RemoveImageExtension_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SelectedImageExt) && Image.Ext.Contains(SelectedImageExt))
            {
                Image.Ext.Remove(SelectedImageExt);
                SelectedImageExt = null; // Clear selection
                OnPropertyChanged(nameof(Image));
            }
            else
            {
                MessageBox.Show("Select an extension to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool ValidateExtension(string input, out string normalizedExt)
        {
            normalizedExt = input?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(normalizedExt))
            {
                MessageBox.Show("Extension cannot be empty.", "Invalid Extension", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Ensure extension starts with a dot and contains valid characters
            if (!normalizedExt.StartsWith("."))
            {
                normalizedExt = "." + normalizedExt;
            }

            var regex = new Regex(@"^\.[a-zA-Z0-9]+$");
            if (!regex.IsMatch(normalizedExt))
            {
                MessageBox.Show("Invalid extension format. Use letters and numbers only (e.g., .txt).", "Invalid Extension", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}