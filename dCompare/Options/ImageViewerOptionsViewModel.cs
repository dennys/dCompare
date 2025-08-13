using CommunityToolkit.Mvvm.ComponentModel;
using dCompare.Configuration;

namespace dCompare.Options
{
    public partial class ImageViewerOptionsViewModel : ObservableObject, IConfigurationUpdater
    {
        private readonly ImageViewerOptions _model;
        public virtual bool IsValid => true;

        public ImageViewerOptionsViewModel(ImageViewerOptions model)
        {
            _model = model;

            _showDiffByDefault = model.ShowDiffByDefault;
            _defaultBlockSize = model.DefaultBlockSize;
            _defaultThreshold = model.DefaultThreshold;
            _showExifCamera = model.ShowExifCamera;
            _showExifLens = model.ShowExifLens;
            _showExifShutterSpeed = model.ShowExifShutterSpeed;
            _showExifAperture = model.ShowExifAperture;
            _showExifIso = model.ShowExifIso;
        }

        [ObservableProperty]
        private bool _showDiffByDefault;

        [ObservableProperty]
        private int _defaultBlockSize;

        [ObservableProperty]
        private int _defaultThreshold;

        [ObservableProperty]
        private bool _showExifCamera;

        [ObservableProperty]
        private bool _showExifLens;

        [ObservableProperty]
        private bool _showExifShutterSpeed;

        [ObservableProperty]
        private bool _showExifAperture;

        [ObservableProperty]
        private bool _showExifIso;

        public void UpdateModel()
        {
            _model.ShowDiffByDefault = ShowDiffByDefault;
            _model.DefaultBlockSize = DefaultBlockSize;
            _model.DefaultThreshold = DefaultThreshold;
            _model.ShowExifCamera = ShowExifCamera;
            _model.ShowExifLens = ShowExifLens;
            _model.ShowExifShutterSpeed = ShowExifShutterSpeed;
            _model.ShowExifAperture = ShowExifAperture;
            _model.ShowExifIso = ShowExifIso;
        }
    }
}
