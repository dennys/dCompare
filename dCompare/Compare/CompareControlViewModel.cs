using CommunityToolkit.Mvvm.ComponentModel;
using dCompare.Configuration;
using System.Collections.ObjectModel;
using System.Globalization;
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

        // True while the directory total is still unknown: the bar runs indeterminate instead of pretending 0%.
        [ObservableProperty]
        private bool _isCountingDirectories;

        [ObservableProperty]
        private string _comparisonProgressText = string.Empty;

        [ObservableProperty]
        private int _directoriesFound;

        [ObservableProperty]
        private ComparisonAlgorithm _selectedAlgorithm;

        /// <summary>
        /// Items for the algorithm dropdown: display text and the algorithm it stands for, the shape the
        /// ComboBox's DisplayMemberPath / SelectedValuePath expect. Filled by <see cref="LoadAlgorithms"/>.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<KeyValuePair<string, ComparisonAlgorithm>> _availableAlgorithms = new();

        // Whether the dropdown was filled once already. A refresh keeps the user's own choice; the first load
        // must not, because SelectedAlgorithm still holds default(enum) there, which is a real algorithm.
        private bool _algorithmsLoaded;

        /// <summary>
        /// Fills the dropdown and picks the default from dCompare.json. The list used to be bound to the static
        /// seed in <see cref="ComparisonAlgorithms"/>, so enabling or disabling an algorithm on the Options page
        /// was lost on the next start and the tab could offer one the user had switched off.
        /// </summary>
        public void LoadAlgorithms(IEnumerable<KeyValuePair<ComparisonAlgorithm, ComparisonAlgorithmInfo>> algorithms)
        {
            List<KeyValuePair<ComparisonAlgorithm, ComparisonAlgorithmInfo>> enabled =
                algorithms.Where(algorithm => algorithm.Value.IsEnabled).ToList();

            AvailableAlgorithms = new ObservableCollection<KeyValuePair<string, ComparisonAlgorithm>>(
                enabled.Select(algorithm => new KeyValuePair<string, ComparisonAlgorithm>(algorithm.Value.Description, algorithm.Key)));

            // A user who already picked one keeps it as long as the Options page still offers it: refreshing the
            // list after Settings | Save must not quietly move the dropdown somewhere else. Only a choice that has
            // been switched off (or was never made) falls back to the configured default, then to the first enabled.
            ComparisonAlgorithm selected = SelectedAlgorithm;
            if (!_algorithmsLoaded || !enabled.Any(algorithm => algorithm.Key == selected))
            {
                selected = enabled.FirstOrDefault(algorithm => algorithm.Value.IsDefault).Key;
                if (!enabled.Any(algorithm => algorithm.Key == selected))
                {
                    selected = enabled.Count > 0 ? enabled[0].Key : ComparisonAlgorithm.ModifiedDateAndSize;
                }
            }
            SelectedAlgorithm = selected;
            _algorithmsLoaded = true;
        }

        /// <summary>
        /// Called before a compare starts: the bar restarts indeterminate and the counter restarts, because 0%
        /// of an unknown total is not a fact and a frozen determinate bar reads as a hang.
        /// </summary>
        public void BeginCompare()
        {
            ComparisonProgress = 0;
            DirectoriesFound = 0;
            IsCountingDirectories = true;
            ComparisonProgressText = string.Format(CultureInfo.CurrentCulture, Properties.Resources.ComparingCountingFormat, 0);
        }

        /// <summary>
        /// Applies one progress report. A report without a fraction only moves the folder counter, and only while
        /// the total is still unknown: once a real percentage has arrived the bar never goes back to being unsure.
        /// </summary>
        public void ReportProgress(DirectoryComparer.CompareProgress progress)
        {
            DirectoriesFound = progress.DirectoriesFound;
            if (progress.Fraction is double fraction)
            {
                IsCountingDirectories = false;
                ComparisonProgress = fraction * 100.0;
                ComparisonProgressText = string.Format(CultureInfo.CurrentCulture, Properties.Resources.ComparingProgressFormat, ComparisonProgress);
            }
            else if (IsCountingDirectories)
            {
                ComparisonProgressText = string.Format(CultureInfo.CurrentCulture, Properties.Resources.ComparingCountingFormat, progress.DirectoriesFound);
            }
        }

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
            Action<bool> setIsSuggestionsOpen = isLeft ? (val => IsLeftSuggestionsOpen = val) : (val => IsRightSuggestionsOpen = val);
            Action<List<string>> setSuggestions = isLeft ? (val => LeftSuggestions = val) : (val => RightSuggestions = val);

            if (string.IsNullOrEmpty(path))
            {
                setIsSuggestionsOpen(false);
                return;
            }

            try
            {
                string? directory = System.IO.Path.GetDirectoryName(path);
                string prefix = System.IO.Path.GetFileName(path);

                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    setIsSuggestionsOpen(false);
                    return;
                }

                var allDirectories = Directory.GetDirectories(directory).ToList();
                var suggestions = allDirectories
                    .Where(dir => System.IO.Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                setSuggestions(suggestions);
                setIsSuggestionsOpen(suggestions.Count != 0);
            }
            catch (Exception)
            {
                setIsSuggestionsOpen(false);
            }
        }
    }
}
