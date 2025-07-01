using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace dCompare.Diff
{
    public partial class UcImageDiffViewer : UserControl, INotifyPropertyChanged
    {
        private string _leftPath = string.Empty;
        private string _rightPath = string.Empty;
        private BitmapImage? _leftImageSource;
        private BitmapImage? _rightImageSource;

        public string LeftPath
        {
            get => _leftPath;
            set
            {
                _leftPath = value;
                OnPropertyChanged(nameof(LeftPath));
                LoadLeftImage();
            }
        }

        public string RightPath
        {
            get => _rightPath;
            set
            {
                _rightPath = value;
                OnPropertyChanged(nameof(RightPath));
                LoadRightImage();
            }
        }

        public BitmapImage? LeftImageSource
        {
            get => _leftImageSource;
            set
            {
                _leftImageSource = value;
                OnPropertyChanged(nameof(LeftImageSource));
            }
        }

        public BitmapImage? RightImageSource
        {
            get => _rightImageSource;
            set
            {
                _rightImageSource = value;
                OnPropertyChanged(nameof(RightImageSource));
            }
        }

        public UcImageDiffViewer(string leftPath, string rightPath)
        {
            InitializeComponent();
            DataContext = this;
            SetImagePaths(leftPath, rightPath);
        }

        private void LoadLeftImage()
        {
            if (!string.IsNullOrEmpty(LeftPath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new System.Uri(LeftPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    LeftImageSource = bitmap;
                }
                catch
                {
                    LeftImageSource = null;
                }
            }
            else
            {
                LeftImageSource = null;
            }
        }

        private void LoadRightImage()
        {
            if (!string.IsNullOrEmpty(RightPath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new System.Uri(RightPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    RightImageSource = bitmap;
                }
                catch
                {
                    RightImageSource = null;
                }
            }
            else
            {
                RightImageSource = null;
            }
        }

        private void SetImagePaths(string leftPath, string rightPath)
        {
            LeftPath = leftPath;
            RightPath = rightPath;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}