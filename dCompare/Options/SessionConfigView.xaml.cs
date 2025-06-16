using dCompare.Configuration;
using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Options
{
    public partial class SessionConfigView : UserControl, INotifyPropertyChanged
    {
        private readonly OptionsConfig _options;

        public SessionConfigView()
        {
            InitializeComponent();
            _options = (DataContext as OptionsConfig) ?? new OptionsConfig();
            DataContext = this; // Set DataContext to this view for binding
        }

        public bool AutoLoadDefaultSession
        {
            get => _options.Session.AutoLoadDefaultSession;
            set
            {
                _options.Session.AutoLoadDefaultSession = value;
                if (!value)
                {
                    AutoCompareAfterSessionLoad = false; // Uncheck the second checkbox
                }
                OnPropertyChanged(nameof(AutoLoadDefaultSession));
            }
        }

        public bool AutoCompareAfterSessionLoad
        {
            get => _options.Session.AutoCompareAfterSessionLoad;
            set
            {
                _options.Session.AutoCompareAfterSessionLoad = value;
                OnPropertyChanged(nameof(AutoCompareAfterSessionLoad));
            }
        }

        public string SessionFilePath // Renamed to avoid conflict
        {
            get => _options.Session.DefaultSessionFile;
            set
            {
                _options.Session.DefaultSessionFile = value;
                OnPropertyChanged(nameof(SessionFilePath));
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select a JSON File",
                AddToRecent = true,
                ClientGuid = Guid.NewGuid()
            };

            if (dialog.ShowDialog() == true)
            {
                SessionFilePath = dialog.FileName; // Update the property
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}