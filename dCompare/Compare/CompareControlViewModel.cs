using CommunityToolkit.Mvvm.ComponentModel;
using dCompare.Configuration;
using System.IO;

namespace dCompare.Compare
{
    public partial class CompareControlViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _leftPath = string.Empty;

        [ObservableProperty]
        private string _rightPath = string.Empty;

        [ObservableProperty]
        private bool _isLeftSuggestionsOpen;

        [ObservableProperty]
        private bool _isRightSuggestionsOpen;

        [ObservableProperty]
        private List<string> _leftSuggestions = new();

        [ObservableProperty]
        private List<string> _rightSuggestions = new();

        [ObservableProperty]
        private string _selectedLeftSuggestion = string.Empty;

        [ObservableProperty]
        private string _selectedRightSuggestion = string.Empty;

        private bool _suppressLeftSuggestions;
        private bool _suppressRightSuggestions;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isComparing;

        [ObservableProperty]
        private double _comparisonProgress;

        [ObservableProperty]
        private ComparisonAlgorithm _selectedAlgorithm;

        partial void OnLeftPathChanged(string value)
        {
            if (!_suppressLeftSuggestions)
            {
                UpdateSuggestions(true);
            }
            _suppressLeftSuggestions = false;
        }

        partial void OnRightPathChanged(string value)
        {
            if (!_suppressRightSuggestions)
            {
                UpdateSuggestions(false);
            }
            _suppressRightSuggestions = false;
        }

        partial void OnSelectedLeftSuggestionChanged(string value)
        {
            if (value != null)
            {
                LeftPath = value;
                IsLeftSuggestionsOpen = false;
            }
        }

        partial void OnSelectedRightSuggestionChanged(string value)
        {
            if (value != null)
            {
                RightPath = value;
                IsRightSuggestionsOpen = false;
            }
        }

        public void SuppressLeftSuggestionsOnce()
        {
            _suppressLeftSuggestions = true;
        }

        public void SuppressRightSuggestionsOnce()
        {
            _suppressRightSuggestions = true;
        }

        private void UpdateSuggestions(bool isLeft)
        {
            var path = isLeft ? LeftPath : RightPath;
            if (string.IsNullOrEmpty(path))
            {
                if (isLeft) IsLeftSuggestionsOpen = false;
                else IsRightSuggestionsOpen = false;
                return;
            }

            try
            {
                string? directory = System.IO.Path.GetDirectoryName(path);
                string prefix = System.IO.Path.GetFileName(path);

                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    if (isLeft) IsLeftSuggestionsOpen = false;
                    else IsRightSuggestionsOpen = false;
                    return;
                }

                var allDirectories = Directory.GetDirectories(directory).ToList();

                var suggestions = allDirectories
                    .Where(dir => System.IO.Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (isLeft)
                {
                    LeftSuggestions = suggestions;
                    IsLeftSuggestionsOpen = suggestions.Count != 0;
                }
                else
                {
                    RightSuggestions = suggestions;
                    IsRightSuggestionsOpen = suggestions.Count != 0;
                }
            }
            catch (Exception)
            {
                if (isLeft) IsLeftSuggestionsOpen = false;
                else IsRightSuggestionsOpen = false;
            }
        }
    }
}
