using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Options
{
    public partial class PreferredToolsSelector : UserControl
    {
        public static readonly DependencyProperty AvailableToolsProperty =
            DependencyProperty.Register("AvailableTools", typeof(IEnumerable<string>), typeof(PreferredToolsSelector), new PropertyMetadata(null));

        public IEnumerable<string> AvailableTools
        {
            get { return (IEnumerable<string>)GetValue(AvailableToolsProperty); }
            set { SetValue(AvailableToolsProperty, value); }
        }

        public static readonly DependencyProperty SelectedToolsProperty =
            DependencyProperty.Register("SelectedTools", typeof(List<string>), typeof(PreferredToolsSelector), new PropertyMetadata(new List<string>()));

        public List<string> SelectedTools
        {
            get { return (List<string>)GetValue(SelectedToolsProperty); }
            set { SetValue(SelectedToolsProperty, value); }
        }

        public PreferredToolsSelector()
        {
            InitializeComponent();
        }
    }
}
