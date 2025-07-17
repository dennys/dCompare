using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace dCompare.Viewer
{
    public partial class ImageViewerViewModel : ObservableObject
    {
        [ObservableProperty]
        private ImageSource _imageSource;

        [ObservableProperty]
        private Stretch _imageStretch = Stretch.Uniform;

        private double _zoomFactor = 1.0;
        private string _currentFilePath;

        public IRelayCommand OpenImageCommand { get; }
        public IRelayCommand SaveImageCommand { get; }
        public IRelayCommand ZoomInCommand { get; }
        public IRelayCommand ZoomOutCommand { get; }
        public IRelayCommand FitToWindowCommand { get; }

        public ImageViewerViewModel()
        {
            OpenImageCommand = new RelayCommand(OpenImage);
            SaveImageCommand = new RelayCommand(SaveImage, CanSaveImage);
            ZoomInCommand = new RelayCommand(ZoomIn);
            ZoomOutCommand = new RelayCommand(ZoomOut);
            FitToWindowCommand = new RelayCommand(FitToWindow);
        }

        public void LoadFile(string filePath)
        {
            _currentFilePath = filePath;
            ImageSource = new BitmapImage(new System.Uri(filePath));
            (SaveImageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        private void OpenImage()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg;*.bmp;*.gif)|*.png;*.jpeg;*.jpg;*.bmp;*.gif|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadFile(openFileDialog.FileName);
            }
        }

        private bool CanSaveImage()
        {
            return !string.IsNullOrEmpty(_currentFilePath);
        }

        private void SaveImage()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Png Image|*.png|Jpg Image|*.jpg|Bitmap Image|*.bmp|Gif Image|*.gif",
                FileName = Path.GetFileName(_currentFilePath)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)ImageSource));

                using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                {
                    encoder.Save(fileStream);
                }
            }
        }

        private void ZoomIn()
        {
            _zoomFactor *= 1.2;
            ApplyZoom();
        }

        private void ZoomOut()
        {
            _zoomFactor /= 1.2;
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            if (ImageSource is BitmapSource bitmapSource)
            {
                var transformedBitmap = new TransformedBitmap(bitmapSource, new ScaleTransform(_zoomFactor, _zoomFactor));
                ImageSource = transformedBitmap;
                ImageStretch = Stretch.None;
            }
        }

        private void FitToWindow()
        {
            _zoomFactor = 1.0;
            ImageStretch = Stretch.Uniform;
            LoadFile(_currentFilePath);
        }
    }
}
