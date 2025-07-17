using System.Windows.Controls;
using System.Windows.Input;

namespace dCompare.Viewer
{
    public partial class ImageViewer : UserControl, IViewer
    {
        public ImageViewer(string filePath)
        {
            InitializeComponent();
            var viewModel = new ImageViewerViewModel();
            DataContext = viewModel;
            viewModel.LoadFile(filePath);

            PreviewMouseWheel += OnPreviewMouseWheel;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var viewModel = DataContext as ImageViewerViewModel;
                if (viewModel == null) return;

                if (e.Delta > 0)
                {
                    viewModel.ZoomIn();
                }
                else
                {
                    viewModel.ZoomOut();
                }
                e.Handled = true;
            }
        }

        private void FitToWindow_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ImageViewerImage.Stretch = Stretch.Uniform;
        }

        public void LoadFile(string filePath)
        {
            (DataContext as ImageViewerViewModel)?.LoadFile(filePath);
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}
