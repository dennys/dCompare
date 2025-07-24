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
        private ImageSource? _imageSource;

        [ObservableProperty]
        private Stretch _imageStretch = Stretch.Uniform;

        [ObservableProperty]
        private double _zoomFactor = 1.0;

        private string? _currentFilePath;

        public IRelayCommand OpenImageCommand { get; }
        public IRelayCommand SaveImageCommand { get; }
        public IRelayCommand ZoomInCommand { get; }
        public IRelayCommand ZoomOutCommand { get; }

        public ImageViewerViewModel()
        {
            OpenImageCommand = new RelayCommand(OpenImage);
            SaveImageCommand = new RelayCommand(SaveImage, CanSaveImage);
            ZoomInCommand = new RelayCommand(ZoomIn);
            ZoomOutCommand = new RelayCommand(ZoomOut);
        }

        public void LoadFile(string filePath)
        {
            try
            {
                _currentFilePath = filePath;
                ImageSource = new BitmapImage(new Uri(filePath));
                (SaveImageCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
            }
        }

        private void OpenImage()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg;*.bmp;*.gif)|*.png;*.jpeg;*.jpg;*.bmp;*.gif|All files (*.*)|*.*",
                InitialDirectory = !string.IsNullOrEmpty(_currentFilePath)
                    ? Path.GetDirectoryName(_currentFilePath)
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        private bool CanSaveImage()
        {
            return !string.IsNullOrEmpty(_currentFilePath) && ImageSource is not null;
        }

        private void SaveImage()
        {
            if (string.IsNullOrEmpty(_currentFilePath))
                return;

            var defaultExt = Path.GetExtension(_currentFilePath)?.ToLower();
            var saveFileDialog = new SaveFileDialog
            {
                FileName = Path.GetFileName(_currentFilePath),
                DefaultExt = defaultExt,
                Filter = $"Same as original (*{defaultExt})|*{defaultExt}|All files (*.*)|*.*"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(_currentFilePath)?.ToLower() ?? ".png";
                    BitmapEncoder? encoder = GetEncoderByExtension(ext);
                    if (encoder == null || ImageSource is not BitmapSource bitmapSource)
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
