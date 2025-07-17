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

        private string _currentFilePath;
        private readonly List<double> _zoomLevels = new List<double> { 0.1, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 4.0, 8.0 };
        private int _currentZoomLevelIndex = 4;

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

        internal void ZoomIn()
        {
            if (_currentZoomLevelIndex < _zoomLevels.Count - 1)
            {
                _currentZoomLevelIndex++;
                ApplyZoom();
            }
        }

        internal void ZoomOut()
        {
            if (_currentZoomLevelIndex > 0)
            {
                _currentZoomLevelIndex--;
                ApplyZoom();
            }
        }

        private void ApplyZoom()
        {
            if (ImageSource is BitmapSource)
            {
                var zoom = _zoomLevels[_currentZoomLevelIndex];
                var transformedBitmap = new TransformedBitmap(new BitmapImage(new System.Uri(_currentFilePath)), new ScaleTransform(zoom, zoom));
                ImageSource = transformedBitmap;
                ImageStretch = Stretch.None;
            }
        }

        private void FitToWindow()
        {
            _currentZoomLevelIndex = 4; // 1.0
            ImageStretch = Stretch.Uniform;
            LoadFile(_currentFilePath);
        }
    }
}
