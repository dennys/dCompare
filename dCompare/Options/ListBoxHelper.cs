using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Options
{
    public static class ListBoxHelper
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached("SelectedItems", typeof(IList), typeof(ListBoxHelper),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

        public static IList GetSelectedItems(DependencyObject d)
        {
            return (IList)d.GetValue(SelectedItemsProperty);
        }

        public static void SetSelectedItems(DependencyObject d, IList value)
        {
            d.SetValue(SelectedItemsProperty, value);
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                listBox.SelectionChanged -= OnSelectionChanged;
                if (e.NewValue != null)
                {
                    listBox.SelectedItems.Clear();
                    foreach (var item in (IList)e.NewValue)
                    {
                        listBox.SelectedItems.Add(item);
                    }
                }
                listBox.SelectionChanged += OnSelectionChanged;
            }
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                var selectedItems = new List<string>();
                foreach (var item in listBox.SelectedItems)
                {
                    selectedItems.Add(item.ToString());
                }
                SetSelectedItems(listBox, selectedItems);
            }
        }
    }
}
