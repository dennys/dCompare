using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace dCompare.Tools.Viewer
{
    public partial class ImageViewerViewModel : ObservableObject
    {
        private const double MinZoom = 0.1;
        private const double MaxZoom = 10.0;

        [ObservableProperty]
        private ImageSource? _leftImageSource;

        [ObservableProperty]
        private ImageSource? _rightImageSource;

        [ObservableProperty]
        private Stretch _imageStretch = Stretch.Uniform;

        [ObservableProperty]
        private double _zoomFactor = 1.0;

        [ObservableProperty]
        private bool _isDiffView;

        [ObservableProperty]
        private string _leftPath = string.Empty;

        [ObservableProperty]
        private string _rightPath = string.Empty;

        public IRelayCommand OpenImageCommand { get; }
        public IRelayCommand SaveImageCommand { get; }
        public IRelayCommand ZoomInCommand { get; }
        public IRelayCommand ZoomOutCommand { get; }
        public IRelayCommand RefreshCommand { get; }

        public ImageViewerViewModel()
        {
            OpenImageCommand = new RelayCommand(OpenImage);
            SaveImageCommand = new RelayCommand(SaveImage, CanSaveImage);
            ZoomInCommand = new RelayCommand(ZoomIn);
            ZoomOutCommand = new RelayCommand(ZoomOut);
            RefreshCommand = new RelayCommand(Refresh);
        }

        public void LoadFile(string filePath, string? rightFilePath = null)
        {
            LeftPath = filePath;
            LeftImageSource = LoadImage(filePath);

            if (rightFilePath != null)
            {
                RightPath = rightFilePath;
                RightImageSource = LoadImage(rightFilePath);
                IsDiffView = true;
            }
            else
            {
                IsDiffView = false;
            }
            (SaveImageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        private void Refresh()
        {
            LoadFile(LeftPath, IsDiffView ? RightPath : null);
        }

        private static BitmapImage? LoadImage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new System.Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
                return null;
            }
        }

        private void OpenImage()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = Properties.Resources.ImageFiles,
                InitialDirectory = !string.IsNullOrEmpty(LeftPath)
                    ? Path.GetDirectoryName(LeftPath)
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        private bool CanSaveImage()
        {
            return !string.IsNullOrEmpty(LeftPath) && LeftImageSource is not null;
        }

        private void SaveImage()
        {
            if (string.IsNullOrEmpty(LeftPath))
                return;

            var defaultExt = Path.GetExtension(LeftPath)?.ToLower();
            var saveFileDialog = new SaveFileDialog
            {
                FileName = Path.GetFileName(LeftPath),
                DefaultExt = defaultExt,
                Filter = string.Format(Properties.Resources.SameAsOriginal, defaultExt)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(LeftPath)?.ToLower() ?? ".png";
                    BitmapEncoder? encoder = GetEncoderByExtension(ext);
                    if (encoder == null || LeftImageSource is not BitmapSource bitmapSource)
                        return;

                    encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                    using var fs = new FileStream(saveFileDialog.FileName!, FileMode.Create);
                    encoder.Save(fs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving image: {ex.Message}");
                }
            }
        }


        private static BitmapEncoder? GetEncoderByExtension(string? extension)
        {
            return extension?.ToLower() switch
            {
                ".png" => new PngBitmapEncoder(),
                ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
                ".bmp" => new BmpBitmapEncoder(),
                ".gif" => new GifBitmapEncoder(),
                _ => null
            };
        }

        internal void ZoomIn()
        {
            if (ZoomFactor < MaxZoom)
                ZoomFactor *= 1.2;
        }

        internal void ZoomOut()
        {
            if (ZoomFactor > MinZoom)
                ZoomFactor /= 1.2;
        }
    }
}
