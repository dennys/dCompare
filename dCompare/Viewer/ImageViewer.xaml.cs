using System.Windows.Controls;

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
