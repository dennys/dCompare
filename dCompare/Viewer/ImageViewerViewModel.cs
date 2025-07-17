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

        [ObservableProperty]
        private ScaleTransform _imageScaleTransform = new ScaleTransform();

        private double _zoomFactor = 1.0;
        private string _currentFilePath;

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
            _zoomFactor *= 1.2;
            ApplyZoom();
        }

        internal void ZoomOut()
        {
            _zoomFactor /= 1.2;
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            ImageScaleTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
            ImageStretch = Stretch.None;
        }

    }
}
