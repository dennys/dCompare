using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using dCompare.Configuration;

namespace dCompare.Options
{
    public partial class GeneralConfigView : UserControl
    {
        public GeneralConfigView()
        {
            InitializeComponent();
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
                    options.DefaultSessionFile = dialog.FileName;
                }
            }
        }
    }
}