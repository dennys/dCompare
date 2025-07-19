using dCompare.Compare;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace dCompare.Tools.Diff
{
    public partial class ImageDiff : UserControl, INotifyPropertyChanged, IRefreshable, ITool
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

        public ImageDiff(string leftPath, string rightPath)
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
        public void Refresh()
        {
            if (string.IsNullOrEmpty(LeftPath) || string.IsNullOrEmpty(RightPath))
            {
                MessageBox.Show("Both image paths must be set to refresh the comparison.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                LoadLeftImage();
                LoadRightImage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh images: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void LoadFile(string filePath)
        {
            // In a comparison view, we load both files, so this method can be a no-op
            // or load the left file path.
            LeftPath = filePath;
            Refresh();
        }

        public void SaveFile(string? path = null)
        {
            throw new NotImplementedException();
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}