using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiffPlex.Wpf;
using DiffPlex.Wpf.Controls;
using System.IO;
using System.Windows;

namespace dCompare.Tools.Diff
{
    public partial class TextDiffPlexViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _leftPath;

        [ObservableProperty]
        private string _rightPath;

        [ObservableProperty]
        private string _leftText = string.Empty;

        [ObservableProperty]
        private string _rightText = string.Empty;

        private readonly DiffViewer _diffView;

        public TextDiffPlexViewModel(string leftPath, string rightPath, DiffViewer diffView)
        {
            _leftPath = leftPath;
            _rightPath = rightPath;
            _diffView = diffView;
            _diffView.IsCommandBarVisible = true;
            Refresh();
        }

        [RelayCommand]
        private void SwitchMode()
        {
            if (_diffView.IsInlineViewMode)
            {
                _diffView.ShowSideBySide();
                return;
            }

            _diffView.ShowInline();
        }

        [RelayCommand]
        private void FurtherActions()
        {
            _diffView.OpenViewModeContextMenu();
        }

        [RelayCommand]
        private static void TestWindow()
        {
            bool has = false;
            foreach (var w in Application.Current.Windows)
            {
                if (w is DiffWindow dw)
                {
                    dw.Activate();
                    has = true;
                    break;
                }
            }

            if (has) return;
            var win = new DiffWindow();
            win.OpenFileOnBoth();
            win.Show();
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(LeftPath) || string.IsNullOrEmpty(RightPath))
            {
                MessageBox.Show(Properties.Resources.ErrorBothFilePaths, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                LeftText = File.ReadAllText(LeftPath);
                RightText = File.ReadAllText(RightPath);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorFailedToRefreshComparison, ex.Message), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadFile(string filePath)
        {
            // TODO this function is non-meaningful
            LeftPath = filePath;
            Refresh();
        }
    }
}
