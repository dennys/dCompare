using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare.Tools.Viewer
{
    public partial class ImageViewer : UserControl, ITool
    {
        private readonly ImageViewerViewModel _viewModel;

        public ImageViewer(string filePath, Configuration.IConfigurationService configService)
        {
            InitializeComponent();
            _viewModel = new ImageViewerViewModel(configService);
            DataContext = _viewModel;
            _ = _viewModel.LoadFileAsync(filePath);

            PreviewMouseWheel += OnPreviewMouseWheel;
        }

        public ImageViewer(string leftFilePath, string rightFilePath, Configuration.IConfigurationService configService)
        {
            InitializeComponent();
            _viewModel = new ImageViewerViewModel(configService);
            DataContext = _viewModel;
            _ = _viewModel.LoadFileAsync(leftFilePath, rightFilePath);

            PreviewMouseWheel += OnPreviewMouseWheel;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    _viewModel.ZoomIn();
                }
                else
                {
                    _viewModel.ZoomOut();
                }
                e.Handled = true;
            }
        }

        public void LoadFile(string filePath)
        {
            _ = _viewModel.LoadFileAsync(filePath);
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}
