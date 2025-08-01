using dCompare.Common;
using System.Windows;
using System.Windows.Controls;

namespace dCompare.Tools.Diff
{
    public partial class ImageDiff : UserControl, IRefreshable, ITool
    {
        private readonly ImageDiffViewModel _viewModel;
        public ImageDiff(string leftPath, string rightPath)
        {
            InitializeComponent();
            _viewModel = new ImageDiffViewModel();
            _viewModel.SetImagePaths(leftPath, rightPath);
            DataContext = _viewModel;
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(_viewModel.LeftPath) || string.IsNullOrEmpty(_viewModel.RightPath))
            {
                MessageBox.Show(Properties.Resources.ErrorBothImagePaths, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _viewModel.SetImagePaths(_viewModel.LeftPath, _viewModel.RightPath);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorFailedToRefreshImages, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadFile(string filePath)
        {
            // In a comparison view, we load both files, so this method can be a no-op
            // or load the left file path.
            _viewModel.LeftPath = filePath;
            Refresh();
        }

        public void SaveFile(string? path = null)
        {
            throw new System.NotImplementedException();
        }

        public UserControl GetControl()
        {
            return this;
        }
    }
}