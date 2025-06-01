using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace dCompare
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<FileSystemNode> LeftItems { get; set; } = new();
        public ObservableCollection<FileSystemNode> RightItems { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            string leftPath = LeftPathBox.Text.Trim();
            string rightPath = RightPathBox.Text.Trim();

            if (!Directory.Exists(leftPath) || !Directory.Exists(rightPath))
            {
                MessageBox.Show("Both directories must exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LeftItems.Clear();
            RightItems.Clear();

            var comparison = DirectoryComparer.CompareDirectories(leftPath, rightPath);

            foreach (var (left, right) in comparison)
            {
                if (left != null) LeftItems.Add(left);
                if (right != null) RightItems.Add(right);
            }
        }

        private void CopyToLeft_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in RightListView.SelectedItems.Cast<FileSystemNode>())
            {
                if (item.Status == ComparisonStatus.OnlyInRight || item.Status == ComparisonStatus.Modified)
                {
                    string target = item.FullPath.Replace(RightPathBox.Text.Trim(), LeftPathBox.Text.Trim());
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                    File.Copy(item.FullPath, target, true);
                }
            }

            Compare_Click(null, null);
        }

        private void CopyToRight_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in LeftListView.SelectedItems.Cast<FileSystemNode>())
            {
                if (item.Status == ComparisonStatus.OnlyInLeft || item.Status == ComparisonStatus.Modified)
                {
                    string target = item.FullPath.Replace(LeftPathBox.Text.Trim(), RightPathBox.Text.Trim());
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                    File.Copy(item.FullPath, target, true);
                }
            }

            Compare_Click(null, null);
        }
    }
}