using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dCompare.Compare;
using Microsoft.Win32;
using System.Collections.ObjectModel;
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

        [ObservableProperty]
        private string? _errorMessage;

        public IRelayCommand OpenImageCommand { get; }
        public IRelayCommand SaveImageCommand { get; }
        public IRelayCommand ZoomInCommand { get; }
        public IRelayCommand ZoomOutCommand { get; }
        public IAsyncRelayCommand RefreshCommand { get; }
        public IRelayCommand ToggleExifVisibilityCommand { get; }
        public IAsyncRelayCommand ShowDiffCommand { get; }

        [ObservableProperty]
        private bool _isDimensionSame;

        [ObservableProperty]
        private bool _isExifVisible;
        [ObservableProperty]
        private string? _leftCamera;
        [ObservableProperty]
        private string? _rightCamera;
        [ObservableProperty]
        private bool _isCameraDifferent;
        [ObservableProperty]
        private string? _leftLens;
        [ObservableProperty]
        private string? _rightLens;
        [ObservableProperty]
        private bool _isLensDifferent;
        [ObservableProperty]
        private string? _leftAperture;
        [ObservableProperty]
        private string? _rightAperture;
        [ObservableProperty]
        private bool _isApertureDifferent;
        [ObservableProperty]
        private string? _leftShutterSpeed;
        [ObservableProperty]
        private string? _rightShutterSpeed;
        [ObservableProperty]
        private bool _isShutterSpeedDifferent;
        [ObservableProperty]
        private string? _leftIso;
        [ObservableProperty]
        private string? _rightIso;
        [ObservableProperty]
        private bool _isIsoDifferent;

        [ObservableProperty]
        private int _blockSize;
        [ObservableProperty]
        private int _threshold;
        [ObservableProperty]
        private bool _isComparingDiffs;
        [ObservableProperty]
        private bool _isDiffResultVisible;

        [ObservableProperty]
        private bool _isExifCameraVisible;
        [ObservableProperty]
        private bool _isExifLensVisible;
        [ObservableProperty]
        private bool _isExifShutterSpeedVisible;
        [ObservableProperty]
        private bool _isExifApertureVisible;
        [ObservableProperty]
        private bool _isExifIsoVisible;

        private readonly Configuration.IConfigurationService _configService;
        private Configuration.ImageViewerOptions _options;

        public ObservableCollection<System.Drawing.Rectangle> DifferenceRectangles { get; } = new();

        public ImageViewerViewModel(Configuration.IConfigurationService configService)
        {
            _configService = configService;

            OpenImageCommand = new RelayCommand(OpenImage);
            SaveImageCommand = new RelayCommand(SaveImage, CanSaveImage);
            ZoomInCommand = new RelayCommand(ZoomIn);
            ZoomOutCommand = new RelayCommand(ZoomOut);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            ToggleExifVisibilityCommand = new RelayCommand(() => IsExifVisible = !IsExifVisible);
            ShowDiffCommand = new AsyncRelayCommand(ToggleDiffVisibility, CanShowDiff);
        }

        private bool CanShowDiff()
        {
            return IsDimensionSame;
        }

        public async Task LoadFileAsync(string filePath, string? rightFilePath = null)
        {
            var config = await _configService.LoadConfigurationAsync();
            _options = config.Options.ImageViewer;

            BlockSize = _options.DefaultBlockSize;
            Threshold = _options.DefaultThreshold;
            IsExifCameraVisible = _options.ShowExifCamera;
            IsExifLensVisible = _options.ShowExifLens;
            IsExifShutterSpeedVisible = _options.ShowExifShutterSpeed;
            IsExifApertureVisible = _options.ShowExifAperture;
            IsExifIsoVisible = _options.ShowExifIso;

            ErrorMessage = null;
            LeftPath = filePath;
            RightPath = rightFilePath ?? string.Empty;
            IsDiffView = rightFilePath != null;

            try
            {
                LeftImageSource = LoadImage(LeftPath);
            }
            catch (Exception ex)
            {
                LeftImageSource = null;
                ErrorMessage = $"Error loading left image: {ex.Message}";
            }

            if (IsDiffView)
            {
                try
                {
                    RightImageSource = LoadImage(RightPath);
                }
                catch (Exception ex)
                {
                    RightImageSource = null;
                    ErrorMessage = $"{ErrorMessage}\nError loading right image: {ex.Message}".Trim();
                }
            }
            else
            {
                RightImageSource = null;
            }

            LoadAllExifData();

            if (IsDiffView && LeftImageSource is BitmapSource leftBitmap && RightImageSource is BitmapSource rightBitmap)
            {
                IsDimensionSame = leftBitmap.PixelWidth == rightBitmap.PixelWidth && leftBitmap.PixelHeight == rightBitmap.PixelHeight;
            }
            else
            {
                IsDimensionSame = false;
            }

            (SaveImageCommand as RelayCommand)?.NotifyCanExecuteChanged();
            ShowDiffCommand.NotifyCanExecuteChanged();

            if (IsDiffView && _options.ShowDiffByDefault && CanShowDiff())
            {
                await ShowDiffCommand.ExecuteAsync(null);
            }
        }
        private async Task ToggleDiffVisibility()
        {
            if (IsComparingDiffs) return;

            if (DifferenceRectangles.Any())
            {
                DifferenceRectangles.Clear();
                IsDiffResultVisible = false;
                return;
            }

            if (LeftImageSource == null || RightImageSource == null) return;

            IsComparingDiffs = true;
            ShowDiffCommand.NotifyCanExecuteChanged();

            try
            {
                var comparer = ImageComparerFactory.CreateImageSharpComparer();
                var options = new ImageComparisonOptions
                {
                    BlockSize = BlockSize,
                    Threshold = Threshold,
                    MergeAdjacent = true,
                    Algorithm = ImageComparisonAlgorithm.EuclideanDistance
                };

                var result = await comparer.CompareImagesAsync(
                    new BitmapImageSource((BitmapSource)LeftImageSource),
                    new BitmapImageSource((BitmapSource)RightImageSource),
                    options);

                foreach (var region in result.DifferenceRegions)
                {
                    // Convert SixLabors.ImageSharp.Rectangle to System.Drawing.Rectangle
                    DifferenceRectangles.Add(new System.Drawing.Rectangle(
                        region.X,
                        region.Y,
                        region.Width,
                        region.Height));
                }
                IsDiffResultVisible = DifferenceRectangles.Any();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error comparing images: {ex.Message}";
            }
            finally
            {
                IsComparingDiffs = false;
                ShowDiffCommand.NotifyCanExecuteChanged();
            }
        }

        private void LoadAllExifData()
        {
            var leftExif = LoadExifData(LeftPath);
            LeftCamera = leftExif.Camera ?? "N/A";
            LeftLens = leftExif.Lens ?? "N/A";
            LeftAperture = leftExif.Aperture ?? "N/A";
            LeftShutterSpeed = leftExif.ShutterSpeed ?? "N/A";
            LeftIso = leftExif.Iso ?? "N/A";

            if (IsDiffView)
            {
                var rightExif = LoadExifData(RightPath);
                RightCamera = rightExif.Camera ?? "N/A";
                RightLens = rightExif.Lens ?? "N/A";
                RightAperture = rightExif.Aperture ?? "N/A";
                RightShutterSpeed = rightExif.ShutterSpeed ?? "N/A";
                RightIso = rightExif.Iso ?? "N/A";
            }
            else
            {
                RightCamera = RightLens = RightAperture = RightShutterSpeed = RightIso = string.Empty;
            }

            IsCameraDifferent = IsDiffView && LeftCamera != RightCamera;
            IsLensDifferent = IsDiffView && LeftLens != RightLens;
            IsApertureDifferent = IsDiffView && LeftAperture != RightAperture;
            IsShutterSpeedDifferent = IsDiffView && LeftShutterSpeed != RightShutterSpeed;
            IsIsoDifferent = IsDiffView && LeftIso != RightIso;
        }

        private static (string? Camera, string? Lens, string? Aperture, string? ShutterSpeed, string? Iso) LoadExifData(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                return (null, null, null, null, null);
            }

            try
            {
                var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(imagePath);
                var subIfdDirectory = directories.OfType<MetadataExtractor.Formats.Exif.ExifSubIfdDirectory>().FirstOrDefault();
                var ifd0Directory = directories.OfType<MetadataExtractor.Formats.Exif.ExifIfd0Directory>().FirstOrDefault();

                var camera = ifd0Directory?.GetDescription(MetadataExtractor.Formats.Exif.ExifIfd0Directory.TagModel);
                var lens = subIfdDirectory?.GetDescription(MetadataExtractor.Formats.Exif.ExifSubIfdDirectory.TagLensModel);
                var aperture = subIfdDirectory?.GetDescription(MetadataExtractor.Formats.Exif.ExifSubIfdDirectory.TagFNumber);
                var shutter = subIfdDirectory?.GetDescription(MetadataExtractor.Formats.Exif.ExifSubIfdDirectory.TagExposureTime);
                var iso = subIfdDirectory?.GetDescription(MetadataExtractor.Formats.Exif.ExifSubIfdDirectory.TagIsoEquivalent);

                return (camera, lens, aperture, shutter, iso);
            }
            catch
            {
                return (null, null, null, null, null);
            }
        }

        private Task RefreshAsync()
        {
            return LoadFileAsync(LeftPath, IsDiffView ? RightPath : null);
        }

        private static BitmapImage LoadImage(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path), "Image path cannot be null or empty.");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Image file not found.", path);
            }

            var bitmap = new BitmapImage();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
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
                _ = LoadFileAsync(openFileDialog.FileName);
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