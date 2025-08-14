using CommunityToolkit.Mvvm.ComponentModel;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace dCompare.Tools.Diff
{
    public partial class TextDiffViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _leftPath = string.Empty;

        [ObservableProperty]
        private string _rightPath = string.Empty;

        [ObservableProperty]
        private ObservableCollection<DiffPiece>? _leftLines;

        [ObservableProperty]
        private ObservableCollection<DiffPiece>? _rightLines;

        public TextDiffViewModel(string leftPath, string rightPath, object comparisonResult)
        {
            SetComparisonResult(leftPath, rightPath, comparisonResult);
        }

        private void SetComparisonResult(string leftPath, string rightPath, object comparisonResult)
        {
            bool EnableVerboseLogging = false;

            LeftPath = leftPath;
            RightPath = rightPath;

            if (comparisonResult is SideBySideDiffModel diffModel)
            {
                var leftLines = new ObservableCollection<DiffPiece>();
                var rightLines = new ObservableCollection<DiffPiece>();

                foreach (var piece in diffModel.OldText.Lines)
                {
                    if (piece.SubPieces == null || piece.SubPieces.Count == 0)
                    {
                        piece.SubPieces = new List<DiffPiece> { new DiffPiece(piece.Text, piece.Type) };
                    }
                    leftLines.Add(piece);
                }

                foreach (var piece in diffModel.NewText.Lines)
                {
                    if (piece.SubPieces == null || piece.SubPieces.Count == 0)
                    {
                        piece.SubPieces = new List<DiffPiece> { new DiffPiece(piece.Text, piece.Type) };
                    }
                    rightLines.Add(piece);
                }

                LeftLines = leftLines;
                RightLines = rightLines;

                if (EnableVerboseLogging)
                {
                    //Debug: Log DiffPiece types and subpieces
                    foreach (var piece in LeftLines)
                    {
                        Debug.WriteLine($"Left DiffPiece: Text='{piece.Text}', Type={piece.Type}, SubPieces={piece.SubPieces?.Count ?? 0}");
                        if (piece.SubPieces != null)
                        {
                            foreach (var subPiece in piece.SubPieces)
                            {
                                Debug.WriteLine($"  SubPiece: Text='{subPiece.Text}', Type={subPiece.Type}");
                            }
                        }
                    }
                    foreach (var piece in RightLines)
                    {
                        Debug.WriteLine($"Right DiffPiece: Text='{piece.Text}', Type={piece.Type}, SubPieces={piece.SubPieces?.Count ?? 0}");
                        if (piece.SubPieces != null)
                        {
                            foreach (var subPiece in piece.SubPieces)
                            {
                                Debug.WriteLine($"  SubPiece: Text='{subPiece.Text}', Type={subPiece.Type}");
                            }
                        }
                    }
                }
            }
            else
            {
                LeftLines = new ObservableCollection<DiffPiece>();
                RightLines = new ObservableCollection<DiffPiece>();
            }
        }

        public void Refresh()
        {
            if (string.IsNullOrEmpty(LeftPath) || string.IsNullOrEmpty(RightPath))
            {
                MessageBox.Show(Properties.Resources.ErrorBothFilePaths, Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Read file contents
                string leftText = File.ReadAllText(LeftPath);
                string rightText = File.ReadAllText(RightPath);

                // Compute diff using DiffPlex
                var diffBuilder = new SideBySideDiffBuilder();
                var diffModel = diffBuilder.BuildDiffModel(leftText, rightText);

                // Update the comparison result
                SetComparisonResult(LeftPath, RightPath, diffModel);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.ErrorFailedToRefreshComparison, ex.Message), Properties.Resources.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
