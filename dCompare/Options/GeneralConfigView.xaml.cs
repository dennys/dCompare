using dCompare.Configuration;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Options
{
    public partial class GeneralConfigView : UserControl
    {
        private bool _autoLoadDefaultSession;
        private bool _autoCompareAfterSessionLoad;
        public GeneralConfigView()
        {
            InitializeComponent();
        }
        public bool AutoLoadDefaultSession
        {
            get => _autoLoadDefaultSession;
            set
            {
                _autoLoadDefaultSession = value;
                if (!_autoLoadDefaultSession)
                {
                    // The following code doesn't work
                    AutoCompareAfterSessionLoad = false; // Uncheck the second checkbox
                    ExecuteComparisonCheckBox.IsChecked = false;
                }
                OnPropertyChanged(Nameof(AutoLoadDefaultSession));
            }
        }

        private DependencyPropertyChangedEventArgs Nameof(bool autoLoadDefaultSession)
        {
            throw new NotImplementedException();
        }

        public bool AutoCompareAfterSessionLoad
        {
            get => _autoCompareAfterSessionLoad;
            set
            {
                _autoCompareAfterSessionLoad = value;
                OnPropertyChanged(Nameof(AutoCompareAfterSessionLoad));
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                if (DataContext is AppConfiguration options)
                {
                    options.Options.DefaultSessionFile = dialog.FileName;
                }
            }
        }
    }
}