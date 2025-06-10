using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using dCompare.Configuration;
using Microsoft.Win32;

namespace dCompare.Options
{
    public partial class OptionsWindow : Window
    {
        private readonly ConfigurationService _configService;
        private readonly OptionsViewModel _viewModel;
        private DispatcherTimer _searchTimer;

        public OptionsWindow()
        {
            InitializeComponent();
            _configService = new ConfigurationService();
            _viewModel = new OptionsViewModel(_configService);
            DataContext = _viewModel;

            // Initialize theme
            UpdateTheme(_viewModel.Options.Theme);

            // Initialize search debounce timer
            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _searchTimer.Tick += SearchTimer_Tick;

            // Select first item by default
            if (OptionsTree.Items.Count > 0)
            {
                var firstItem = (TreeViewItem)OptionsTree.Items[0];
                firstItem.IsSelected = true;
            }
        }


        private void UpdateTheme(string theme)
        {
            var resourceDict = new ResourceDictionary();
            if (theme.ToLower() == "dark")
            {
                resourceDict.Add("WindowBackground", Resources["WindowBackground.Dark"]);
                resourceDict.Add("ControlBackground", Resources["ControlBackground.Dark"]);
                resourceDict.Add("TextColor", Resources["TextColor.Dark"]);
            }
            else
            {
                resourceDict.Add("WindowBackground", Resources["WindowBackground"]);
                resourceDict.Add("ControlBackground", Resources["ControlBackground"]);
                resourceDict.Add("TextColor", Resources["TextColor"]);
            }
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(resourceDict);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void SearchTimer_Tick(object sender, EventArgs e)
        {
            _searchTimer.Stop();
            _viewModel.FilterTreeItems();
        }

        private void OptionsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag != null)
            {
                _viewModel.SetCurrentConfigView(item.Tag.ToString());
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            _configService.UpdateOptions(_viewModel.Options);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}