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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<FileSystemNode> LeftRoot { get; set; } = new();
        public ObservableCollection<FileSystemNode> RightRoot { get; set; } = new();
        public ObservableCollection<FileSystemNode> BaseRoot { get; set; } = new();

        private bool _isThreeWay = false;
        public bool IsThreeWay
        {
            get => _isThreeWay;
            set { _isThreeWay = value; OnPropertyChanged(nameof(IsThreeWay)); }
        }

        public MainWindow()
        {
            InitializeComponent();
            //DataContext = this;

            //// Load sample directories here
            //LoadDirectory("D:\\bin\\compress\\7-Zip", LeftRoot);
            //LoadDirectory("D:\\bin\\compress\\PeaZIP", RightRoot);
            //LoadDirectory("D:\\bin\\compress\\WinRAR", BaseRoot);
            //IsThreeWay = true; // toggle this
        }

        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            string leftPath = "D:\\git\\dCompare";
            string rightPath = "D:\\git\\dCompare_xx";

            var results = DirectoryComparer.CompareDirectories(leftPath, rightPath);

            LeftList.ItemsSource = results.Select(r => r.Left ?? new FileSystemNode { Name = "", Status = ComparisonStatus.OnlyInRight });
            RightList.ItemsSource = results.Select(r => r.Right ?? new FileSystemNode { Name = "", Status = ComparisonStatus.OnlyInLeft });
        }

        private void LoadDirectory(string path, ObservableCollection<FileSystemNode> target)
        {
            if (!Directory.Exists(path))
            {
                MessageBox.Show($"Directory not found: {path}");
                return;
            }

            target.Clear();
            var root = BuildNode(path);
            target.Add(root);
        }

        private FileSystemNode BuildNode(string path)
        {
            var node = new FileSystemNode
            {
                Name = System.IO.Path.GetFileName(path),
                FullPath = path,
                //Created = File.GetCreationTime(path),
                Created = File.GetLastWriteTime(path),
                Size = Directory.Exists(path) ? 0 : new FileInfo(path).Length,
                IsDirectory = Directory.Exists(path)
            };

            if (node.IsDirectory)
            {
                foreach (var dir in Directory.GetDirectories(path))
                    node.Children.Add(BuildNode(dir));
                foreach (var file in Directory.GetFiles(path))
                    node.Children.Add(BuildNode(file));
            }

            return node;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}