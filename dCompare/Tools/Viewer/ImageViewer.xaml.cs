using dCompare.Common;
using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare.Tools.Viewer
{
    public partial class ImageViewer : UserControl, ITool
    {
        private readonly ImageViewerViewModel _viewModel;

        public ImageViewer(string filePath)
        {
            InitializeComponent();
            _viewModel = new ImageViewerViewModel();
            DataContext = _viewModel;
            _viewModel.LoadFile(filePath);

            PreviewMouseWheel += OnPreviewMouseWheel;
        }

        public ImageViewer(string leftFilePath, string rightFilePath)
        {
            InitializeComponent();
            _viewModel = new ImageViewerViewModel();
            DataContext = _viewModel;
            _viewModel.LoadFile(leftFilePath, rightFilePath);

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
            _viewModel.LoadFile(filePath);
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}
