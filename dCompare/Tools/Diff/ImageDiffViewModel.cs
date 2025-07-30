using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;

namespace dCompare.Tools.Diff
{
    public partial class ImageDiffViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _leftPath = string.Empty;

        [ObservableProperty]
        private string _rightPath = string.Empty;

        [ObservableProperty]
        private BitmapImage? _leftImageSource;

        [ObservableProperty]
        private BitmapImage? _rightImageSource;

        public ImageDiffViewModel()
        {
        }

        public void SetImagePaths(string leftPath, string rightPath)
        {
            LeftPath = leftPath;
            RightPath = rightPath;
            LoadImages();
        }

        private void LoadImages()
        {
            LeftImageSource = LoadImage(LeftPath);
            RightImageSource = LoadImage(RightPath);
        }

        private static BitmapImage? LoadImage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new System.Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
